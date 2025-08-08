using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

namespace RealtimeDialog.Core.Protocol
{
    /// <summary>
    /// 检查MsgTypeFlagBits是否表示序列化数据中存在序列号的函数类型
    /// </summary>
    /// <param name="bits">消息类型标志位</param>
    /// <returns>是否包含序列号</returns>
    public delegate bool ContainsSequenceFunc(MsgTypeFlagBits bits);

    /// <summary>
    /// 执行压缩操作的函数类型
    /// </summary>
    /// <param name="data">要压缩的数据</param>
    /// <returns>压缩后的数据</returns>
    public delegate byte[] CompressFunc(byte[] data);

    /// <summary>
    /// 实现Lab-Speech MDD、TTS、ASR等服务中使用的二进制协议序列化和反序列化
    /// </summary>
    public class BinaryProtocol
    {
        private readonly ILogger<BinaryProtocol>? _logger;
        private byte _versionAndHeaderSize;
        private byte _serializationAndCompression;
        private ContainsSequenceFunc? _containsSequence;
        private CompressFunc? _compress;

        private static readonly HashSet<SerializationBits> ValidSerializations = new()
        {
            SerializationBits.Raw,
            SerializationBits.JSON,
            SerializationBits.Thrift,
            SerializationBits.Custom
        };

        private static readonly HashSet<CompressionBits> ValidCompressions = new()
        {
            CompressionBits.None,
            CompressionBits.Gzip,
            CompressionBits.Custom
        };

        public BinaryProtocol(ILogger<BinaryProtocol>? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// 反序列化二进制数据为Message和BinaryProtocol
        /// </summary>
        /// <param name="data">二进制数据</param>
        /// <param name="containsSequence">序列号检查函数</param>
        /// <param name="logger">日志记录器</param>
        /// <returns>消息和协议实例</returns>
        /// <exception cref="InvalidDataException">数据格式无效</exception>
        public static (Message message, BinaryProtocol protocol) Unmarshal(byte[] data, ContainsSequenceFunc containsSequence, ILogger<BinaryProtocol>? logger = null)
        {
            using var stream = new MemoryStream(data);
            using var reader = new BinaryReader(stream);
            
            int readSize = 0;

            // 读取版本和头部大小
            if (stream.Position >= stream.Length)
                throw new InvalidDataException("No protocol version and header size byte");
            
            byte versionSize = reader.ReadByte();
            readSize++;

            var protocol = new BinaryProtocol(logger)
            {
                _versionAndHeaderSize = versionSize,
                _containsSequence = containsSequence
            };

            logger?.LogDebug("Read version: {Version:X}", versionSize >> 4);
            logger?.LogDebug("Read size: {Size:X}", versionSize & 0b1111);

            // 读取消息类型和标志
            if (stream.Position >= stream.Length)
                throw new InvalidDataException("No message type and specific flag byte");
            
            byte typeAndFlag = reader.ReadByte();
            readSize++;
            logger?.LogDebug("Read message type: {Type:X}", typeAndFlag >> 4);
            logger?.LogDebug("Read message type specific flag: {Flag:X}", typeAndFlag & 0b1111);

            var message = Message.CreateMessageFromByte(typeAndFlag);

            // 读取序列化和压缩方法
            if (stream.Position >= stream.Length)
                throw new InvalidDataException("No serialization and compression method byte");
            
            byte serializationCompression = reader.ReadByte();
            logger?.LogDebug("Read serialization method: {Serialization:X}", serializationCompression >> 4);
            logger?.LogDebug("Read compression method: {Compression:X}", serializationCompression & 0b1111);
            readSize++;
            
            protocol._serializationAndCompression = serializationCompression;
            
            if (!ValidSerializations.Contains(protocol.Serialization))
                throw new InvalidDataException($"Invalid serialization bits: {protocol.Serialization:b}");
            
            if (!ValidCompressions.Contains(protocol.Compression))
                throw new InvalidDataException($"Invalid compression bits: {protocol.Compression:b}");

            // 读取头部填充字节
            int paddingSize = protocol.HeaderSize - readSize;
            if (paddingSize > 0)
            {
                byte[] padding = reader.ReadBytes(paddingSize);
                if (padding.Length < paddingSize)
                    throw new InvalidDataException($"No enough header bytes: {padding.Length}");
            }

            // 读取消息内容
            ReadMessageContent(message, reader, containsSequence, logger);

            // 检查是否有多余字节
            if (stream.Position < stream.Length)
                throw new InvalidDataException("There are redundant bytes in data");

            return (message, protocol);
        }

        /// <summary>
        /// 序列化消息为二进制数据
        /// </summary>
        /// <param name="message">要序列化的消息</param>
        /// <returns>序列化后的二进制数据</returns>
        public byte[] Marshal(Message message)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            // 写入头部
            WriteHeader(writer, message);

            // 压缩负载（如果需要）
            byte[] payload = message.Payload;
            if (_compress != null)
            {
                payload = _compress(payload);
            }

            // 写入消息内容
            WriteMessageContent(message, writer, payload);

            return stream.ToArray();
        }

        private static void ReadMessageContent(Message message, BinaryReader reader, ContainsSequenceFunc containsSequence, ILogger<BinaryProtocol>? logger)
        {
            // 根据消息类型决定读取顺序
            switch (message.Type)
            {
                case MsgType.AudioOnlyClient:
                    if (containsSequence?.Invoke(message.TypeFlag) == true)
                    {
                        message.Sequence = reader.ReadInt32();
                        if (BitConverter.IsLittleEndian)
                            message.Sequence = BinaryPrimitives.ReverseEndianness(message.Sequence);
                        logger?.LogDebug("AudioOnlyClient message: read Sequence {Sequence}", message.Sequence);
                    }
                    break;

                case MsgType.AudioOnlyServer:
                    if (containsSequence?.Invoke(message.TypeFlag) == true)
                    {
                        message.Sequence = reader.ReadInt32();
                        if (BitConverter.IsLittleEndian)
                            message.Sequence = BinaryPrimitives.ReverseEndianness(message.Sequence);
                        logger?.LogDebug("AudioOnlyServer message: read Sequence {Sequence}", message.Sequence);
                    }
                    break;

                case MsgType.Error:
                    message.ErrorCode = reader.ReadUInt32();
                    if (BitConverter.IsLittleEndian)
                        message.ErrorCode = BinaryPrimitives.ReverseEndianness(message.ErrorCode);
                    logger?.LogDebug("Error message: read ErrorCode {ErrorCode}", message.ErrorCode);
                    break;
            }

            // 读取事件和会话信息
            if (Message.ContainsEvent(message.TypeFlag))
            {
                message.Event = reader.ReadInt32();
                if (BitConverter.IsLittleEndian)
                    message.Event = BinaryPrimitives.ReverseEndianness(message.Event);
                logger?.LogDebug("Read Event: {Event}", message.Event);

                // 读取SessionID
                if (!Message.ShouldSkipSessionID(message.Event))
                {
                    uint sessionIdSize = reader.ReadUInt32();
                    if (BitConverter.IsLittleEndian)
                        sessionIdSize = BinaryPrimitives.ReverseEndianness(sessionIdSize);
                    
                    if (sessionIdSize > 0)
                    {
                        byte[] sessionIdBytes = reader.ReadBytes((int)sessionIdSize);
                        message.SessionID = Encoding.UTF8.GetString(sessionIdBytes);
                    }
                    logger?.LogDebug("Read SessionID: {SessionID}", message.SessionID);
                }

                // 读取ConnectID
                if (Message.ShouldReadConnectID(message.Event))
                {
                    uint connectIdSize = reader.ReadUInt32();
                    if (BitConverter.IsLittleEndian)
                        connectIdSize = BinaryPrimitives.ReverseEndianness(connectIdSize);
                    
                    if (connectIdSize > 0)
                    {
                        byte[] connectIdBytes = reader.ReadBytes((int)connectIdSize);
                        message.ConnectID = Encoding.UTF8.GetString(connectIdBytes);
                    }
                    logger?.LogDebug("Read ConnectID: {ConnectID}", message.ConnectID);
                }
            }

            // 读取负载
            uint payloadSize = reader.ReadUInt32();
            if (BitConverter.IsLittleEndian)
                payloadSize = BinaryPrimitives.ReverseEndianness(payloadSize);
            
            if (payloadSize > 0)
            {
                message.Payload = reader.ReadBytes((int)payloadSize);
            }
            
            if (message.Type is MsgType.FullClient or MsgType.FullServer or MsgType.Error)
            {
                logger?.LogDebug("Read Payload content: {Payload}", Encoding.UTF8.GetString(message.Payload));
            }
        }

        private void WriteMessageContent(Message message, BinaryWriter writer, byte[] payload)
        {
            // 根据消息类型决定写入顺序
            if (_containsSequence?.Invoke(message.TypeFlag) == true)
            {
                int sequence = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(message.Sequence) : message.Sequence;
                writer.Write(sequence);
                _logger?.LogDebug("Write Sequence: {Sequence}", message.Sequence);
            }

            if (Message.ContainsEvent(message.TypeFlag))
            {
                int eventValue = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(message.Event) : message.Event;
                writer.Write(eventValue);
                _logger?.LogDebug("Write Event: {Event}", message.Event);

                // 写入SessionID
                if (!Message.ShouldSkipSessionID(message.Event))
                {
                    byte[] sessionIdBytes = Encoding.UTF8.GetBytes(message.SessionID);
                    uint sessionIdSize = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness((uint)sessionIdBytes.Length) : (uint)sessionIdBytes.Length;
                    writer.Write(sessionIdSize);
                    writer.Write(sessionIdBytes);
                }
            }

            if (message.Type == MsgType.Error)
            {
                uint errorCode = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(message.ErrorCode) : message.ErrorCode;
                writer.Write(errorCode);
            }

            // 写入负载
            uint payloadSize = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness((uint)payload.Length) : (uint)payload.Length;
            writer.Write(payloadSize);
            writer.Write(payload);
        }

        private void WriteHeader(BinaryWriter writer, Message message)
        {
            byte[] header = CreateHeader(message);
            writer.Write(header);
        }

        private byte[] CreateHeader(Message message)
        {
            var header = new List<byte>
            {
                _versionAndHeaderSize,
                message.TypeAndFlagBits,
                _serializationAndCompression
            };

            int padding = HeaderSize - header.Count;
            if (padding > 0)
            {
                header.AddRange(new byte[padding]);
            }

            return header.ToArray();
        }

        #region Properties

        /// <summary>
        /// 设置协议版本
        /// </summary>
        public void SetVersion(VersionBits version)
        {
            _versionAndHeaderSize = (byte)((_versionAndHeaderSize & 0b00001111) | (byte)version);
        }

        /// <summary>
        /// 获取协议版本
        /// </summary>
        public int Version => _versionAndHeaderSize >> 4;

        /// <summary>
        /// 设置协议头部大小
        /// </summary>
        public void SetHeaderSize(HeaderSizeBits headerSize)
        {
            _versionAndHeaderSize = (byte)((_versionAndHeaderSize & 0b11110000) | (byte)headerSize);
        }

        /// <summary>
        /// 获取协议头部大小
        /// </summary>
        public int HeaderSize => 4 * (_versionAndHeaderSize & 0b00001111);

        /// <summary>
        /// 设置序列化方法
        /// </summary>
        public void SetSerialization(SerializationBits serialization)
        {
            _serializationAndCompression = (byte)((_serializationAndCompression & 0b00001111) | (byte)serialization);
        }

        /// <summary>
        /// 获取序列化方法
        /// </summary>
        public SerializationBits Serialization => (SerializationBits)(_serializationAndCompression & 0b11110000);

        /// <summary>
        /// 设置压缩方法
        /// </summary>
        public void SetCompression(CompressionBits compression, CompressFunc? compressFunc = null)
        {
            _serializationAndCompression = (byte)((_serializationAndCompression & 0b11110000) | (byte)compression);
            _compress = compressFunc;
        }

        /// <summary>
        /// 获取压缩方法
        /// </summary>
        public CompressionBits Compression => (CompressionBits)(_serializationAndCompression & 0b00001111);

        /// <summary>
        /// 设置序列号检查函数
        /// </summary>
        public void SetContainsSequenceFunc(ContainsSequenceFunc containsSequence)
        {
            _containsSequence = containsSequence;
        }

        #endregion

        /// <summary>
        /// 克隆当前BinaryProtocol实例
        /// </summary>
        public BinaryProtocol Clone()
        {
            return new BinaryProtocol(_logger)
            {
                _versionAndHeaderSize = _versionAndHeaderSize,
                _serializationAndCompression = _serializationAndCompression,
                _containsSequence = _containsSequence,
                _compress = _compress
            };
        }
    }
}