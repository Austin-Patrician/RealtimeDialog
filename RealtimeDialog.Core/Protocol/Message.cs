using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RealtimeDialog.Core.Protocol
{
    /// <summary>
    /// 定义通用消息内容类型
    /// </summary>
    public class Message
    {
        private static readonly Dictionary<MsgType, byte> MsgTypeToBits = new()
        {
            { MsgType.FullClient, 0b1 << 4 },
            { MsgType.AudioOnlyClient, 0b10 << 4 },
            { MsgType.FullServer, 0b1001 << 4 },
            { MsgType.AudioOnlyServer, 0b1011 << 4 },
            { MsgType.FrontEndResultServer, 0b1100 << 4 },
            { MsgType.Error, 0b1111 << 4 }
        };

        private static readonly Dictionary<byte, MsgType> BitsToMsgType;

        static Message()
        {
            BitsToMsgType = new Dictionary<byte, MsgType>();
            foreach (var kvp in MsgTypeToBits)
            {
                BitsToMsgType[kvp.Value] = kvp.Key;
            }
        }

        public MsgType Type { get; set; }
        public byte TypeAndFlagBits { get; set; }
        public int Event { get; set; }
        public string SessionID { get; set; } = string.Empty;
        public string ConnectID { get; set; } = string.Empty;
        public int Sequence { get; set; }
        public uint ErrorCode { get; set; }
        /// <summary>
        /// 原始负载数据（未经Gzip压缩）。BinaryProtocol.Marshal会为您进行压缩。
        /// </summary>
        public byte[] Payload { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// 返回消息类型特定标志
        /// </summary>
        public MsgTypeFlagBits TypeFlag => (MsgTypeFlagBits)(TypeAndFlagBits & 0b00001111);

        /// <summary>
        /// 创建指定消息类型和特定标志的新Message实例
        /// </summary>
        /// <param name="msgType">消息类型</param>
        /// <param name="typeFlag">类型标志</param>
        /// <returns>新的Message实例</returns>
        /// <exception cref="ArgumentException">无效的消息类型</exception>
        public static Message CreateMessage(MsgType msgType, MsgTypeFlagBits typeFlag)
        {
            if (!MsgTypeToBits.TryGetValue(msgType, out byte bits))
            {
                throw new ArgumentException($"Invalid message type: {msgType}", nameof(msgType));
            }

            return new Message
            {
                Type = msgType,
                TypeAndFlagBits = (byte)(bits + (byte)typeFlag)
            };
        }

        /// <summary>
        /// 从字节读取消息类型和特定标志位，并从中组成新的Message实例
        /// </summary>
        /// <param name="typeAndFlag">类型和标志字节</param>
        /// <returns>新的Message实例</returns>
        /// <exception cref="ArgumentException">无效的消息类型</exception>
        public static Message CreateMessageFromByte(byte typeAndFlag)
        {
            byte bits = (byte)(typeAndFlag & 0b11110000);
            if (!BitsToMsgType.TryGetValue(bits, out MsgType msgType))
            {
                throw new ArgumentException($"Invalid message type bits: {bits >> 4:b}", nameof(typeAndFlag));
            }

            return new Message
            {
                Type = msgType,
                TypeAndFlagBits = typeAndFlag
            };
        }

        /// <summary>
        /// 检查消息类型特定标志是否表示消息包含序列号
        /// </summary>
        /// <param name="bits">标志位</param>
        /// <returns>是否包含序列号</returns>
        public static bool ContainsSequence(MsgTypeFlagBits bits)
        {
            return (bits & MsgTypeFlagBits.PositiveSeq) == MsgTypeFlagBits.PositiveSeq ||
                   (bits & MsgTypeFlagBits.NegativeSeq) == MsgTypeFlagBits.NegativeSeq;
        }

        /// <summary>
        /// 检查是否包含事件信息
        /// </summary>
        /// <param name="bits">标志位</param>
        /// <returns>是否包含事件</returns>
        public static bool ContainsEvent(MsgTypeFlagBits bits)
        {
            return (bits & MsgTypeFlagBits.WithEvent) == MsgTypeFlagBits.WithEvent;
        }

        /// <summary>
        /// 检查特定事件是否需要跳过SessionID
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <returns>是否跳过SessionID</returns>
        public static bool ShouldSkipSessionID(int eventType)
        {
            return eventType is 1 or 2 or 50 or 51 or 52;
        }

        /// <summary>
        /// 检查特定事件是否需要读取ConnectID
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <returns>是否需要ConnectID</returns>
        public static bool ShouldReadConnectID(int eventType)
        {
            return eventType is 50 or 51 or 52;
        }
    }
}