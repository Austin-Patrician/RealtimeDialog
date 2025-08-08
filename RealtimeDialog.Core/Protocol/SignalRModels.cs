using System.ComponentModel.DataAnnotations;

namespace RealtimeDialog.Core.Protocol;

// 音频配置
public class AudioConfig
{
    public int SampleRate { get; set; } = 24000;
    public int Channels { get; set; } = 1;
    public string Format { get; set; } = "float32";
    public int BufferSize { get; set; } = 1024;
}

// 开始对话请求
public class StartConversationRequest
{
    [Required]
    public string SessionId { get; set; } = string.Empty;
    
    [Required]
    public AudioConfig AudioConfig { get; set; } = new();
    
    public string? UserId { get; set; }
}

// 音频数据请求
public class AudioDataRequest
{
    [Required]
    public string SessionId { get; set; } = string.Empty;
    
    [Required]
    public byte[] AudioData { get; set; } = Array.Empty<byte>();
    
    public long Timestamp { get; set; }
    public int SequenceNumber { get; set; }
}

// 连接确认响应
public class ConnectionConfirmResponse
{
    public string SessionId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // "connected" | "error"
    public string? ErrorMessage { get; set; }
    public AudioConfig AudioConfig { get; set; } = new();
}

// 音频数据响应
public class AudioDataResponse
{
    public string SessionId { get; set; } = string.Empty;
    public byte[] AudioData { get; set; } = Array.Empty<byte>();
    public long Timestamp { get; set; }
    public int SequenceNumber { get; set; }
}

// 转录响应
public class TranscriptionResponse
{
    public string SessionId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public bool IsUser { get; set; }
    public long Timestamp { get; set; }
}

// 状态更新响应
public class StatusUpdateResponse
{
    public string SessionId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // "listening" | "processing" | "speaking" | "idle"
    public Dictionary<string, object>? Metadata { get; set; }
}

// 错误响应
public class ErrorResponse
{
    public string SessionId { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public long Timestamp { get; set; }
}

// 会话状态枚举
public enum SessionStatus
{
    Initializing,
    Active,
    Listening,
    Processing,
    Speaking,
    Idle,
    Ended,
    Error
}

// 音频方向枚举
public enum AudioDirection
{
    Incoming, // 从前端接收
    Outgoing  // 发送到前端
}

// 会话实体
public class ConversationSession
{
    public string SessionId { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime LastActivity { get; set; }
    public SessionStatus Status { get; set; }
    public AudioConfig AudioConfig { get; set; } = new();
    public string ConnectionId { get; set; } = string.Empty;
    public System.Net.WebSockets.ClientWebSocket? DoubaoWebSocket { get; set; }
}

// 音频缓冲区
public class AudioBuffer
{
    public string SessionId { get; set; } = string.Empty;
    public int SequenceNumber { get; set; }
    public byte[] AudioData { get; set; } = Array.Empty<byte>();
    public long Timestamp { get; set; }
    public AudioDirection Direction { get; set; }
}