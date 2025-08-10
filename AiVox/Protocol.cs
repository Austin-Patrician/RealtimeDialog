using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AiVox
{
    // Error messages to keep parity with Go version semantics
    internal static class ProtocolErrors
    {
        public const string ErrNoVersionAndSize = "no protocol version and header size byte";
        public const string ErrNoTypeAndFlag = "no message type and specific flag byte";
        public const string ErrNoSerializationAndCompression = "no serialization and compression method byte";
        public const string ErrRedundantBytes = "there are redundant bytes in data";
        public const string ErrInvalidMessageType = "invalid message type bits";
        public const string ErrInvalidSerialization = "invalid serialization bits";
        public const string ErrInvalidCompression = "invalid compression bits";
        public const string ErrNoEnoughHeaderBytes = "no enough header bytes";
        public const string ErrReadEvent = "read event number";
        public const string ErrReadSessionIDSize = "read session ID size";
        public const string ErrReadConnectIDSize = "read connection ID size";
        public const string ErrReadPayloadSize = "read payload size";
        public const string ErrReadPayload = "read payload";
        public const string ErrReadSequence = "read sequence number";
        public const string ErrReadErrorCode = "read error code";
    }

    // Functional types
    internal delegate bool ContainsSequenceFunc(MsgTypeFlagBits bits);
    internal delegate byte[] CompressFunc(byte[] data);

    // Enums mapping exactly to Go bit layout
    internal enum MsgType : int
    {
        MsgTypeInvalid = 0,
        MsgTypeFullClient,
        MsgTypeAudioOnlyClient,
        MsgTypeFullServer,
        MsgTypeAudioOnlyServer,
        MsgTypeFrontEndResultServer,
        MsgTypeError,

        MsgTypeServerACK = MsgTypeAudioOnlyServer,
    }

    [Flags]
    internal enum MsgTypeFlagBits : byte
    {
        // common protocol
        MsgTypeFlagNoSeq = 0,           // 0000
        MsgTypeFlagPositiveSeq = 0b0001, // Non-terminal packet with sequence > 0
        MsgTypeFlagLastNoSeq = 0b0010,   // last packet with no sequence
        MsgTypeFlagNegativeSeq = 0b0011, // last packet with sequence < 0
        MsgTypeFlagWithEvent = 0b0100,   // Payload contains event number (int32)
    }

    internal enum VersionBits : byte
    {
        // (iota+1) << 4
        Version1 = (1 << 4),
        Version2 = (2 << 4),
        Version3 = (3 << 4),
        Version4 = (4 << 4),
    }

    internal enum HeaderSizeBits : byte
    {
        HeaderSize4 = 1, // 1 * 4 = 4 bytes
        HeaderSize8,
        HeaderSize12,
        HeaderSize16,
    }

    internal enum SerializationBits : byte
    {
        SerializationRaw = 0,
        SerializationJSON = (1 << 4),
        SerializationThrift = (3 << 4),
        SerializationCustom = (15 << 4),
    }

    internal enum CompressionBits : byte
    {
        CompressionNone = 0,
        CompressionGzip = 0b0001,
        CompressionCustom = 0b1111,
    }

    internal static class ProtocolLookups
    {
        internal static readonly Dictionary<MsgType, byte> MsgTypeToBits = new()
        {
            { MsgType.MsgTypeFullClient,           (byte)(0b0001 << 4) }, // 0x10
            { MsgType.MsgTypeAudioOnlyClient,      (byte)(0b0010 << 4) }, // 0x20
            { MsgType.MsgTypeFullServer,           (byte)(0b1001 << 4) }, // 0x90
            { MsgType.MsgTypeAudioOnlyServer,      (byte)(0b1011 << 4) }, // 0xB0
            { MsgType.MsgTypeFrontEndResultServer, (byte)(0b1100 << 4) }, // 0xC0
            { MsgType.MsgTypeError,                (byte)(0b1111 << 4) }, // 0xF0
        };

        internal static readonly Dictionary<byte, MsgType> BitsToMsgType;
        internal static readonly HashSet<SerializationBits> Serializations = new()
        {
            SerializationBits.SerializationRaw,
            SerializationBits.SerializationJSON,
            SerializationBits.SerializationThrift,
            SerializationBits.SerializationCustom
        };

        internal static readonly HashSet<CompressionBits> Compressions = new()
        {
            CompressionBits.CompressionNone,
            CompressionBits.CompressionGzip,
            CompressionBits.CompressionCustom
        };

        static ProtocolLookups()
        {
            BitsToMsgType = new Dictionary<byte, MsgType>(MsgTypeToBits.Count);
            foreach (var kv in MsgTypeToBits)
            {
                BitsToMsgType[kv.Value] = kv.Key;
            }
        }
    }

    internal static class ProtocolHelpers
    {
        internal static bool ContainsSequence(MsgTypeFlagBits bits)
        {
            return (bits & MsgTypeFlagBits.MsgTypeFlagPositiveSeq) == MsgTypeFlagBits.MsgTypeFlagPositiveSeq
                   || (bits & MsgTypeFlagBits.MsgTypeFlagNegativeSeq) == MsgTypeFlagBits.MsgTypeFlagNegativeSeq;
        }

        internal static bool ContainsEvent(MsgTypeFlagBits bits)
        {
            return (bits & MsgTypeFlagBits.MsgTypeFlagWithEvent) == MsgTypeFlagBits.MsgTypeFlagWithEvent;
        }
    }

    internal sealed class Message
    {
        public MsgType Type { get; private set; }
        private byte _typeAndFlagBits;

        public int Event;          // int32
        public string SessionID = string.Empty;
        public string ConnectID = string.Empty;
        public int Sequence;       // int32
        public uint ErrorCode;     // uint32
        public byte[] Payload = Array.Empty<byte>();

        public MsgTypeFlagBits TypeFlag()
        {
            return (MsgTypeFlagBits)((byte)(_typeAndFlagBits & 0x0F));
        }

        private List<Func<MemoryStream, Exception?>> Writers(CompressFunc? compress, ContainsSequenceFunc containsSequence)
        {
            // Apply compression if provided (exception propagates)
            if (compress != null && Payload.Length > 0)
            {
                Payload = compress(Payload);
            }

            var writers = new List<Func<MemoryStream, Exception?>>(4);

            if (containsSequence(TypeFlag()))
            {
                writers.Add(WriteSequence);
            }

            if (ProtocolHelpers.ContainsEvent(TypeFlag()))
            {
                writers.Add(WriteEvent);
                writers.Add(WriteSessionID);
            }

            writers.Add(WritePayload);
            return writers;
        }

        private Exception? WriteEvent(MemoryStream ms)
        {
            Span<byte> buf = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(buf, Event);
            ms.Write(buf);
            return null;
        }

        private Exception? WriteSessionID(MemoryStream ms)
        {
            // Skip writing session id for event: 1,2,50,51,52
            if (Event == 1 || Event == 2 || Event == 50 || Event == 51 || Event == 52)
            {
                return null;
            }
            var bytes = Encoding.UTF8.GetBytes(SessionID ?? string.Empty);
            if ((long)bytes.Length > uint.MaxValue)
            {
                return new Exception($"payload size ({bytes.Length}) exceeds max(uint32)");
            }
            Span<byte> sizeBuf = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(sizeBuf, (uint)bytes.Length);
            ms.Write(sizeBuf);
            if (bytes.Length > 0) ms.Write(bytes, 0, bytes.Length);
            return null;
        }

        private Exception? WriteSequence(MemoryStream ms)
        {
            Span<byte> buf = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(buf, Sequence);
            ms.Write(buf);
            return null;
        }

        private Exception? WriteErrorCode(MemoryStream ms)
        {
            Span<byte> buf = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(buf, ErrorCode);
            ms.Write(buf);
            return null;
        }

        private Exception? WritePayload(MemoryStream ms)
        {
            if ((long)Payload.LongLength > uint.MaxValue)
            {
                return new Exception($"payload size ({Payload.LongLength}) exceeds max(uint32)");
            }
            Span<byte> sizeBuf = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(sizeBuf, (uint)Payload.Length);
            ms.Write(sizeBuf);
            if (Payload.Length > 0) ms.Write(Payload, 0, Payload.Length);
            return null;
        }

        private List<Func<ReadOnlySpan<byte>, (int read, Exception? error)>> Readers(ContainsSequenceFunc? containsSequence)
        {
            var readers = new List<Func<ReadOnlySpan<byte>, (int, Exception?)>>(4);

            switch (Type)
            {
                case MsgType.MsgTypeFullClient:
                case MsgType.MsgTypeFullServer:
                case MsgType.MsgTypeFrontEndResultServer:
                    // no seq by default
                    break;

                case MsgType.MsgTypeAudioOnlyClient:
                    if (containsSequence == null || containsSequence(TypeFlag()))
                    {
                        readers.Add(ReadSequence);
                    }
                    break;

                case MsgType.MsgTypeAudioOnlyServer:
                    if (containsSequence != null && containsSequence(TypeFlag()))
                    {
                        readers.Add(ReadSequence);
                    }
                    break;

                case MsgType.MsgTypeError:
                    readers.Add(ReadErrorCode);
                    break;

                default:
                    throw new Exception($"cannot deserialize message with invalid type: {Type}");
            }

            if (ProtocolHelpers.ContainsEvent(TypeFlag()))
            {
                readers.Add(ReadEvent);
                readers.Add(ReadSessionID);
                readers.Add(ReadConnectID);
            }

            readers.Add(ReadPayload);
            return readers;
        }

        private (int read, Exception? error) ReadEvent(ReadOnlySpan<byte> span)
        {
            if (span.Length < 4) return (0, new Exception($"{ProtocolErrors.ErrReadEvent}: EOF"));
            Event = BinaryPrimitives.ReadInt32BigEndian(span);
            return (4, null);
        }

        private (int read, Exception? error) ReadSessionID(ReadOnlySpan<byte> span)
        {
            // Skip reading session id for event: 1,2,50,51,52
            if (Event == 1 || Event == 2 || Event == 50 || Event == 51 || Event == 52)
            {
                return (0, null);
            }
            if (span.Length < 4) return (0, new Exception($"{ProtocolErrors.ErrReadSessionIDSize}: EOF"));
            uint size = BinaryPrimitives.ReadUInt32BigEndian(span);
            if (span.Length < 4 + size) return (0, new Exception($"{ProtocolErrors.ErrReadSessionIDSize}: short buffer"));
            if (size > 0)
            {
                SessionID = Encoding.UTF8.GetString(span.Slice(4, (int)size));
            }
            return (4 + (int)size, null);
        }

        private (int read, Exception? error) ReadConnectID(ReadOnlySpan<byte> span)
        {
            // Only for 50,51,52
            if (!(Event == 50 || Event == 51 || Event == 52))
            {
                return (0, null);
            }
            if (span.Length < 4) return (0, new Exception($"{ProtocolErrors.ErrReadConnectIDSize}: EOF"));
            uint size = BinaryPrimitives.ReadUInt32BigEndian(span);
            if (span.Length < 4 + size) return (0, new Exception($"{ProtocolErrors.ErrReadConnectIDSize}: short buffer"));
            if (size > 0)
            {
                ConnectID = Encoding.UTF8.GetString(span.Slice(4, (int)size));
            }
            return (4 + (int)size, null);
        }

        private (int read, Exception? error) ReadSequence(ReadOnlySpan<byte> span)
        {
            if (span.Length < 4) return (0, new Exception($"{ProtocolErrors.ErrReadSequence}: EOF"));
            Sequence = BinaryPrimitives.ReadInt32BigEndian(span);
            return (4, null);
        }

        private (int read, Exception? error) ReadErrorCode(ReadOnlySpan<byte> span)
        {
            if (span.Length < 4) return (0, new Exception($"{ProtocolErrors.ErrReadErrorCode}: EOF"));
            ErrorCode = BinaryPrimitives.ReadUInt32BigEndian(span);
            return (4, null);
        }

        private (int read, Exception? error) ReadPayload(ReadOnlySpan<byte> span)
        {
            if (span.Length < 4) return (0, new Exception($"{ProtocolErrors.ErrReadPayloadSize}: EOF"));
            uint size = BinaryPrimitives.ReadUInt32BigEndian(span);
            if (span.Length < 4 + size) return (0, new Exception($"{ProtocolErrors.ErrReadPayload}: short buffer"));
            if (size > 0)
            {
                Payload = span.Slice(4, (int)size).ToArray();
            }
            else
            {
                Payload = Array.Empty<byte>();
            }
            return (4 + (int)size, null);
        }

        public static Message NewMessage(MsgType msgType, MsgTypeFlagBits typeFlag)
        {
            if (!ProtocolLookups.MsgTypeToBits.TryGetValue(msgType, out var bits))
            {
                throw new Exception($"invalid message type: {(int)msgType}");
            }
            return new Message
            {
                Type = msgType,
                _typeAndFlagBits = (byte)(bits + (byte)typeFlag)
            };
        }

        public static Message NewMessageFromByte(byte typeAndFlag)
        {
            byte bits = (byte)(typeAndFlag & 0xF0); // clear lower 4 bits
            if (!ProtocolLookups.BitsToMsgType.TryGetValue(bits, out var msgType))
            {
                throw new Exception($"{ProtocolErrors.ErrInvalidMessageType}: {Convert.ToString(bits >> 4, 2)}");
            }
            return new Message
            {
                Type = msgType,
                _typeAndFlagBits = typeAndFlag
            };
        }
    }

    internal sealed class BinaryProtocol
    {
        private byte _versionAndHeaderSize;
        private byte _serializationAndCompression;

        internal ContainsSequenceFunc? containsSequence;
        private CompressFunc? _compress;

        public BinaryProtocol Clone()
        {
            return new BinaryProtocol
            {
                _versionAndHeaderSize = _versionAndHeaderSize,
                _serializationAndCompression = _serializationAndCompression,
                containsSequence = containsSequence,
                _compress = _compress
            };
        }

        public void SetVersion(VersionBits v)
        {
            _versionAndHeaderSize = (byte)((_versionAndHeaderSize & 0x0F) + (byte)v);
        }

        public int Version()
        {
            return _versionAndHeaderSize >> 4;
        }

        public void SetHeaderSize(HeaderSizeBits s)
        {
            _versionAndHeaderSize = (byte)((_versionAndHeaderSize & 0xF0) + (byte)s);
        }

        public int HeaderSize()
        {
            // 4 * low 4 bits
            return 4 * (_versionAndHeaderSize & 0x0F);
        }

        public void SetSerialization(SerializationBits s)
        {
            _serializationAndCompression = (byte)((_serializationAndCompression & 0x0F) + (byte)s);
        }

        public SerializationBits Serialization()
        {
            // Clear lower 4 bits, keep upper nibble
            return (SerializationBits)((byte)(_serializationAndCompression & 0xF0));
        }

        public void SetCompression(CompressionBits c, CompressFunc? f)
        {
            _serializationAndCompression = (byte)((_serializationAndCompression & 0xF0) + (byte)c);
            _compress = f;
        }

        public CompressionBits Compression()
        {
            // Clear upper 4 bits, keep lower nibble
            return (CompressionBits)((byte)(_serializationAndCompression & 0x0F));
        }

        public byte[] Marshal(Message msg)
        {
            using var ms = new MemoryStream();
            // write header
            WriteHeader(ms, msg);

            // write body
            var writers = msg.GetType().GetMethod("Writers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (writers == null)
                throw new Exception("writers method not found");
            var list = (List<Func<MemoryStream, Exception?>>)writers.Invoke(msg, new object?[] { _compress, containsSequence ?? ProtocolHelpers.ContainsSequence })!;
            foreach (var w in list)
            {
                var err = w(ms);
                if (err != null) throw err;
            }

            return ms.ToArray();
        }

        private void WriteHeader(MemoryStream ms, Message msg)
        {
            // header: version/size, type/flag, serialization/compression, then padding
            ms.WriteByte(_versionAndHeaderSize);
            // get private _typeAndFlagBits via reflection to avoid extra public surface
            var field = typeof(Message).GetField("_typeAndFlagBits", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            ms.WriteByte((byte)field!.GetValue(msg)!);
            ms.WriteByte(_serializationAndCompression);

            int headerLen = 3;
            int pad = HeaderSize() - headerLen;
            if (pad > 0)
            {
                ms.Write(new byte[pad], 0, pad);
            }
        }

        public static (Message msg, BinaryProtocol prot) Unmarshal(byte[] data, ContainsSequenceFunc containsSequence)
        {
            int index = 0;
            if (data.Length - index < 1) throw new Exception(ProtocolErrors.ErrNoVersionAndSize);
            byte versionSize = data[index++];
            var prot = new BinaryProtocol
            {
                _versionAndHeaderSize = versionSize,
                containsSequence = containsSequence
            };

            if (data.Length - index < 1) throw new Exception(ProtocolErrors.ErrNoTypeAndFlag);
            byte typeAndFlag = data[index++];

            var msg = Message.NewMessageFromByte(typeAndFlag);

            if (data.Length - index < 1) throw new Exception(ProtocolErrors.ErrNoSerializationAndCompression);
            byte serComp = data[index++];
            prot._serializationAndCompression = serComp;

            if (!ProtocolLookups.Serializations.Contains(prot.Serialization()))
                throw new Exception($"{ProtocolErrors.ErrInvalidSerialization}: {(byte)prot.Serialization()}");
            if (!ProtocolLookups.Compressions.Contains(prot.Compression()))
                throw new Exception($"{ProtocolErrors.ErrInvalidCompression}: {(byte)prot.Compression()}");

            int readSize = 3;
            int paddingSize = prot.HeaderSize() - readSize;
            if (paddingSize > 0)
            {
                if (data.Length - index < paddingSize)
                    throw new Exception($"{ProtocolErrors.ErrNoEnoughHeaderBytes}: {Math.Max(0, data.Length - index)}");
                index += paddingSize;
            }

            // Build readers
            var readersMethod = typeof(Message).GetMethod("Readers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var readers = (List<Func<ReadOnlySpan<byte>, (int read, Exception? error)>>)readersMethod!.Invoke(msg, new object?[] { containsSequence })!;

            foreach (var reader in readers)
            {
                var (read, error) = reader(new ReadOnlySpan<byte>(data, index, data.Length - index));
                if (error != null) throw error;
                index += read;
            }

            if (index != data.Length)
            {
                throw new Exception(ProtocolErrors.ErrRedundantBytes);
            }

            return (msg, prot);
        }
    }

    // Keep exported helper to align with Go's global function naming
    internal static class ProtocolExported
    {
        // For Protocol.cs users: same signature as Go's Unmarshal(data, containsSequence)
        public static (Message msg, BinaryProtocol prot) Unmarshal(byte[] data, ContainsSequenceFunc containsSequence)
        {
            return BinaryProtocol.Unmarshal(data, containsSequence);
        }

        public static BinaryProtocol NewBinaryProtocol()
        {
            return new BinaryProtocol();
        }
    }
}
