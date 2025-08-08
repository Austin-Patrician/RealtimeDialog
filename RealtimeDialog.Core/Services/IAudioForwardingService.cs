using RealtimeDialog.Core.Protocol;

namespace RealtimeDialog.Core.Services;

public interface IAudioForwardingService
{
    /// <summary>
    /// 音频数据接收事件
    /// </summary>
    event EventHandler<AudioDataReceivedEventArgs>? AudioDataReceived;
    
    /// <summary>
    /// 转录文本接收事件
    /// </summary>
    event EventHandler<TranscriptionReceivedEventArgs>? TranscriptionReceived;
    
    /// <summary>
    /// 状态变化事件
    /// </summary>
    event EventHandler<StatusChangedEventArgs>? StatusChanged;
    
    /// <summary>
    /// 错误事件
    /// </summary>
    event EventHandler<ErrorOccurredEventArgs>? ErrorOccurred;
    
    /// <summary>
    /// 连接到豆包API
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="audioConfig">音频配置</param>
    /// <returns>是否连接成功</returns>
    Task<bool> ConnectAsync(string sessionId, AudioConfig audioConfig);
    
    /// <summary>
    /// 发送音频数据到豆包API
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="audioData">音频数据</param>
    /// <param name="timestamp">时间戳</param>
    Task SendAudioDataAsync(string sessionId, byte[] audioData, long timestamp);
    
    /// <summary>
    /// 断开与豆包API的连接
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    Task DisconnectAsync(string sessionId);
    
    /// <summary>
    /// 检查连接状态
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <returns>是否已连接</returns>
    Task<bool> IsConnectedAsync(string sessionId);
}

// 事件参数类
public class AudioDataReceivedEventArgs : EventArgs
{
    public string SessionId { get; set; } = string.Empty;
    public byte[] AudioData { get; set; } = Array.Empty<byte>();
    public long Timestamp { get; set; }
    public int SequenceNumber { get; set; }
}

public class TranscriptionReceivedEventArgs : EventArgs
{
    public string SessionId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public bool IsUser { get; set; }
    public long Timestamp { get; set; }
}

public class StatusChangedEventArgs : EventArgs
{
    public string SessionId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Dictionary<string, object>? Metadata { get; set; }
}

public class ErrorOccurredEventArgs : EventArgs
{
    public string SessionId { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
}