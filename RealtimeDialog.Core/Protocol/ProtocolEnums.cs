using System;

namespace RealtimeDialog.Core.Protocol
{
    /// <summary>
    /// 定义消息类型，决定消息如何通过协议序列化
    /// </summary>
    public enum MsgType : int
    {
        Invalid = 0,
        FullClient = 1,
        AudioOnlyClient = 2,
        FullServer = 3,
        AudioOnlyServer = 4,
        FrontEndResultServer = 5,
        Error = 6
    }

    /// <summary>
    /// 定义4位消息类型特定标志位
    /// </summary>
    public enum MsgTypeFlagBits : byte
    {
        /// <summary>
        /// 无序列号的非终端数据包
        /// </summary>
        NoSeq = 0,
        /// <summary>
        /// 序列号 > 0 的非终端数据包
        /// </summary>
        PositiveSeq = 0b1,
        /// <summary>
        /// 无序列号的最后数据包
        /// </summary>
        LastNoSeq = 0b10,
        /// <summary>
        /// 序列号 < 0 的最后数据包
        /// </summary>
        NegativeSeq = 0b11,
        /// <summary>
        /// 负载包含事件编号 (int32)
        /// </summary>
        WithEvent = 0b100
    }

    /// <summary>
    /// 定义4位版本类型
    /// </summary>
    public enum VersionBits : byte
    {
        Version1 = 1 << 4,
        Version2 = 2 << 4,
        Version3 = 3 << 4,
        Version4 = 4 << 4
    }

    /// <summary>
    /// 定义4位头部大小类型
    /// </summary>
    public enum HeaderSizeBits : byte
    {
        HeaderSize4 = 1,
        HeaderSize8 = 2,
        HeaderSize12 = 3,
        HeaderSize16 = 4
    }

    /// <summary>
    /// 定义4位序列化方法类型
    /// </summary>
    public enum SerializationBits : byte
    {
        Raw = 0,
        JSON = 0b1 << 4,
        Thrift = 0b11 << 4,
        Custom = 0b1111 << 4
    }

    /// <summary>
    /// 定义4位压缩方法类型
    /// </summary>
    public enum CompressionBits : byte
    {
        None = 0,
        Gzip = 0b1,
        Custom = 0b1111
    }

    /// <summary>
    /// MsgType扩展方法
    /// </summary>
    public static class MsgTypeExtensions
    {
        public static string GetDisplayName(this MsgType msgType)
        {
            return msgType switch
            {
                MsgType.FullClient => "FullClient",
                MsgType.AudioOnlyClient => "AudioOnlyClient",
                MsgType.FullServer => "FullServer",
                MsgType.AudioOnlyServer => "AudioOnlyServer/ServerACK",
                MsgType.Error => "Error",
                MsgType.FrontEndResultServer => "TtsFrontEndResult",
                _ => $"invalid message type: {(int)msgType}"
            };
        }
    }
}