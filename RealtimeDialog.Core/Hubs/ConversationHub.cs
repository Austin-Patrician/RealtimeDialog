using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using RealtimeDialog.Core.Protocol;
using RealtimeDialog.Core.Services;

namespace RealtimeDialog.Core.Hubs;

public class ConversationHub : Hub
{
    private readonly ISessionManager _sessionManager;
    private readonly IAudioForwardingService _audioForwardingService;
    private readonly ILogger<ConversationHub> _logger;

    public ConversationHub(
        ISessionManager sessionManager,
        IAudioForwardingService audioForwardingService,
        ILogger<ConversationHub> logger)
    {
        _sessionManager = sessionManager;
        _audioForwardingService = audioForwardingService;
        _logger = logger;
        
        // 订阅音频转发服务的事件
        _audioForwardingService.AudioDataReceived += OnAudioDataReceived;
        _audioForwardingService.TranscriptionReceived += OnTranscriptionReceived;
        _audioForwardingService.StatusChanged += OnStatusChanged;
        _audioForwardingService.ErrorOccurred += OnErrorOccurred;
    }

    /// <summary>
    /// 开始对话会话
    /// </summary>
    /// <param name="request">开始对话请求</param>
    public async Task StartConversation(StartConversationRequest request)
    {
        try
        {
            _logger.LogInformation("Starting conversation for session {SessionId}", request.SessionId);
            
            // 创建会话
            var session = await _sessionManager.CreateSessionAsync(request.SessionId, request.AudioConfig, request.UserId);
            
            // 设置连接ID
            await _sessionManager.SetConnectionIdAsync(request.SessionId, Context.ConnectionId);
            
            // 加入SignalR组
            await Groups.AddToGroupAsync(Context.ConnectionId, request.SessionId);
            
            // 连接到豆包API
            var connected = await _audioForwardingService.ConnectAsync(request.SessionId, request.AudioConfig);
            
            if (connected)
            {
                await _sessionManager.UpdateSessionStatusAsync(request.SessionId, SessionStatus.Active);
                
                // 发送连接确认
                await Clients.Caller.SendAsync("ConnectionConfirm", new ConnectionConfirmResponse
                {
                    SessionId = request.SessionId,
                    Status = "connected",
                    AudioConfig = request.AudioConfig
                });
                
                _logger.LogInformation("Successfully started conversation for session {SessionId}", request.SessionId);
            }
            else
            {
                await _sessionManager.UpdateSessionStatusAsync(request.SessionId, SessionStatus.Error);
                
                await Clients.Caller.SendAsync("ConnectionConfirm", new ConnectionConfirmResponse
                {
                    SessionId = request.SessionId,
                    Status = "error",
                    ErrorMessage = "Failed to connect to Doubao API",
                    AudioConfig = request.AudioConfig
                });
                
                _logger.LogError("Failed to connect to Doubao API for session {SessionId}", request.SessionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting conversation for session {SessionId}", request.SessionId);
            
            await Clients.Caller.SendAsync("ConnectionConfirm", new ConnectionConfirmResponse
            {
                SessionId = request.SessionId,
                Status = "error",
                ErrorMessage = ex.Message,
                AudioConfig = request.AudioConfig
            });
        }
    }

    /// <summary>
    /// 发送音频数据
    /// </summary>
    /// <param name="request">音频数据请求</param>
    public async Task SendAudioData(AudioDataRequest request)
    {
        try
        {
            // 检查会话是否存在且活跃
            var isActive = await _sessionManager.IsSessionActiveAsync(request.SessionId);
            if (!isActive)
            {
                _logger.LogWarning("Received audio data for inactive session {SessionId}", request.SessionId);
                return;
            }
            
            // 更新会话活动时间
            await _sessionManager.UpdateLastActivityAsync(request.SessionId);
            
            // 转发音频数据到豆包API
            await _audioForwardingService.SendAudioDataAsync(request.SessionId, request.AudioData, request.Timestamp);
            
            _logger.LogDebug("Forwarded audio data for session {SessionId}, size: {Size} bytes", 
                request.SessionId, request.AudioData.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing audio data for session {SessionId}", request.SessionId);
            
            await Clients.Group(request.SessionId).SendAsync("ErrorNotification", new ErrorResponse
            {
                SessionId = request.SessionId,
                ErrorCode = "AUDIO_PROCESSING_ERROR",
                ErrorMessage = ex.Message,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        }
    }

    /// <summary>
    /// 停止对话
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    public async Task StopConversation(string sessionId)
    {
        try
        {
            _logger.LogInformation("Stopping conversation for session {SessionId}", sessionId);
            
            // 断开豆包API连接
            await _audioForwardingService.DisconnectAsync(sessionId);
            
            // 更新会话状态
            await _sessionManager.UpdateSessionStatusAsync(sessionId, SessionStatus.Ended);
            
            // 从SignalR组中移除
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionId);
            
            // 发送状态更新
            await Clients.Group(sessionId).SendAsync("StatusUpdate", new StatusUpdateResponse
            {
                SessionId = sessionId,
                Status = "ended"
            });
            
            // 清理会话
            await _sessionManager.RemoveSessionAsync(sessionId);
            
            _logger.LogInformation("Successfully stopped conversation for session {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping conversation for session {SessionId}", sessionId);
        }
    }

    /// <summary>
    /// 获取会话状态
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    public async Task GetSessionStatus(string sessionId)
    {
        try
        {
            var session = await _sessionManager.GetSessionAsync(sessionId);
            if (session != null)
            {
                await Clients.Caller.SendAsync("StatusUpdate", new StatusUpdateResponse
                {
                    SessionId = sessionId,
                    Status = session.Status.ToString().ToLowerInvariant(),
                    Metadata = new Dictionary<string, object>
                    {
                        ["startTime"] = session.StartTime,
                        ["lastActivity"] = session.LastActivity,
                        ["userId"] = session.UserId ?? "anonymous"
                    }
                });
            }
            else
            {
                await Clients.Caller.SendAsync("ErrorNotification", new ErrorResponse
                {
                    SessionId = sessionId,
                    ErrorCode = "SESSION_NOT_FOUND",
                    ErrorMessage = "Session not found",
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting session status for {SessionId}", sessionId);
        }
    }

    /// <summary>
    /// 连接断开时的处理
    /// </summary>
    /// <param name="exception">异常信息</param>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
            
            // 查找并清理相关会话
            var activeSessions = await _sessionManager.GetActiveSessionsAsync();
            var sessionToCleanup = activeSessions.FirstOrDefault(s => s.ConnectionId == Context.ConnectionId);
            
            if (sessionToCleanup != null)
            {
                _logger.LogInformation("Cleaning up session {SessionId} for disconnected client", sessionToCleanup.SessionId);
                await StopConversation(sessionToCleanup.SessionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling client disconnection for {ConnectionId}", Context.ConnectionId);
        }
        
        await base.OnDisconnectedAsync(exception);
    }

    // 事件处理方法
    private async void OnAudioDataReceived(object? sender, AudioDataReceivedEventArgs e)
    {
        try
        {
            await Clients.Group(e.SessionId).SendAsync("ReceiveAudioData", new AudioDataResponse
            {
                SessionId = e.SessionId,
                AudioData = e.AudioData,
                Timestamp = e.Timestamp,
                SequenceNumber = e.SequenceNumber
            });
            
            _logger.LogDebug("Sent audio data to client for session {SessionId}, size: {Size} bytes", 
                e.SessionId, e.AudioData.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending audio data to client for session {SessionId}", e.SessionId);
        }
    }

    private async void OnTranscriptionReceived(object? sender, TranscriptionReceivedEventArgs e)
    {
        try
        {
            await Clients.Group(e.SessionId).SendAsync("ReceiveTranscription", new TranscriptionResponse
            {
                SessionId = e.SessionId,
                Text = e.Text,
                IsUser = e.IsUser,
                Timestamp = e.Timestamp
            });
            
            _logger.LogDebug("Sent transcription to client for session {SessionId}: {Text}", e.SessionId, e.Text);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending transcription to client for session {SessionId}", e.SessionId);
        }
    }

    private async void OnStatusChanged(object? sender, StatusChangedEventArgs e)
    {
        try
        {
            await Clients.Group(e.SessionId).SendAsync("StatusUpdate", new StatusUpdateResponse
            {
                SessionId = e.SessionId,
                Status = e.Status,
                Metadata = e.Metadata
            });
            
            _logger.LogDebug("Sent status update to client for session {SessionId}: {Status}", e.SessionId, e.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending status update to client for session {SessionId}", e.SessionId);
        }
    }

    private async void OnErrorOccurred(object? sender, ErrorOccurredEventArgs e)
    {
        try
        {
            await Clients.Group(e.SessionId).SendAsync("ErrorNotification", new ErrorResponse
            {
                SessionId = e.SessionId,
                ErrorCode = e.ErrorCode,
                ErrorMessage = e.ErrorMessage,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
            
            _logger.LogError("Sent error notification to client for session {SessionId}: {ErrorCode} - {ErrorMessage}", 
                e.SessionId, e.ErrorCode, e.ErrorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending error notification to client for session {SessionId}", e.SessionId);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // 取消订阅事件
            _audioForwardingService.AudioDataReceived -= OnAudioDataReceived;
            _audioForwardingService.TranscriptionReceived -= OnTranscriptionReceived;
            _audioForwardingService.StatusChanged -= OnStatusChanged;
            _audioForwardingService.ErrorOccurred -= OnErrorOccurred;
        }
        
        base.Dispose(disposing);
    }
}