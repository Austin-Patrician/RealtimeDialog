using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RealtimeDialog.Core.Protocol;

namespace RealtimeDialog.Core.Services;

public class DoubaoAudioForwardingService : IAudioForwardingService, IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DoubaoAudioForwardingService> _logger;
    private readonly ConcurrentDictionary<string, ClientWebSocket> _connections = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokens = new();
    private int _sequenceNumber = 0;

    public event EventHandler<AudioDataReceivedEventArgs>? AudioDataReceived;
    public event EventHandler<TranscriptionReceivedEventArgs>? TranscriptionReceived;
    public event EventHandler<StatusChangedEventArgs>? StatusChanged;
    public event EventHandler<ErrorOccurredEventArgs>? ErrorOccurred;

    public DoubaoAudioForwardingService(IConfiguration configuration, ILogger<DoubaoAudioForwardingService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> ConnectAsync(string sessionId, AudioConfig audioConfig)
    {
        try
        {
            var webSocket = new ClientWebSocket();
            var cancellationTokenSource = new CancellationTokenSource();
            
            // 从配置获取豆包API地址
            var doubaoUrl = _configuration["DoubaoApi:Url"] ?? "wss://openspeech.bytedance.com/api/v1/tts";
            var apiKey = _configuration["DoubaoApi:ApiKey"] ?? "";
            
            // 设置请求头
            if (!string.IsNullOrEmpty(apiKey))
            {
                webSocket.Options.SetRequestHeader("Authorization", $"Bearer {apiKey}");
            }
            
            await webSocket.ConnectAsync(new Uri(doubaoUrl), cancellationTokenSource.Token);
            
            _connections.TryAdd(sessionId, webSocket);
            _cancellationTokens.TryAdd(sessionId, cancellationTokenSource);
            
            // 启动消息接收循环
            _ = Task.Run(() => ReceiveMessagesAsync(sessionId, webSocket, cancellationTokenSource.Token));
            
            _logger.LogInformation("Connected to Doubao API for session {SessionId}", sessionId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Doubao API for session {SessionId}", sessionId);
            OnErrorOccurred(sessionId, "CONNECTION_FAILED", ex.Message, ex);
            return false;
        }
    }

    public async Task SendAudioDataAsync(string sessionId, byte[] audioData, long timestamp)
    {
        if (!_connections.TryGetValue(sessionId, out var webSocket) || webSocket.State != WebSocketState.Open)
        {
            _logger.LogWarning("WebSocket not connected for session {SessionId}", sessionId);
            return;
        }

        try
        {
            // 构造发送给豆包API的消息格式
            var message = new
            {
                type = "audio",
                session_id = sessionId,
                audio_data = Convert.ToBase64String(audioData),
                timestamp = timestamp,
                sequence_number = Interlocked.Increment(ref _sequenceNumber)
            };

            var json = JsonSerializer.Serialize(message);
            var buffer = Encoding.UTF8.GetBytes(json);

            if (_cancellationTokens.TryGetValue(sessionId, out var cts) && !cts.Token.IsCancellationRequested)
            {
                await webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, cts.Token);
                _logger.LogDebug("Sent audio data to Doubao API for session {SessionId}, size: {Size} bytes", sessionId, audioData.Length);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send audio data for session {SessionId}", sessionId);
            OnErrorOccurred(sessionId, "SEND_FAILED", ex.Message, ex);
        }
    }

    public async Task DisconnectAsync(string sessionId)
    {
        try
        {
            if (_cancellationTokens.TryRemove(sessionId, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }

            if (_connections.TryRemove(sessionId, out var webSocket))
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Session ended", CancellationToken.None);
                }
                webSocket.Dispose();
            }

            _logger.LogInformation("Disconnected from Doubao API for session {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting from Doubao API for session {SessionId}", sessionId);
        }
    }

    public Task<bool> IsConnectedAsync(string sessionId)
    {
        var isConnected = _connections.TryGetValue(sessionId, out var webSocket) && webSocket.State == WebSocketState.Open;
        return Task.FromResult(isConnected);
    }

    private async Task ReceiveMessagesAsync(string sessionId, ClientWebSocket webSocket, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        
        try
        {
            while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await ProcessReceivedMessageAsync(sessionId, message);
                }
                else if (result.MessageType == WebSocketMessageType.Binary)
                {
                    // 处理二进制音频数据
                    var audioData = new byte[result.Count];
                    Array.Copy(buffer, audioData, result.Count);
                    OnAudioDataReceived(sessionId, audioData, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogInformation("WebSocket closed by server for session {SessionId}", sessionId);
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Receive operation cancelled for session {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error receiving messages for session {SessionId}", sessionId);
            OnErrorOccurred(sessionId, "RECEIVE_FAILED", ex.Message, ex);
        }
    }

    private async Task ProcessReceivedMessageAsync(string sessionId, string message)
    {
        try
        {
            using var document = JsonDocument.Parse(message);
            var root = document.RootElement;
            
            if (root.TryGetProperty("type", out var typeElement))
            {
                var messageType = typeElement.GetString();
                
                switch (messageType)
                {
                    case "audio_response":
                        await HandleAudioResponseAsync(sessionId, root);
                        break;
                    case "transcription":
                        await HandleTranscriptionAsync(sessionId, root);
                        break;
                    case "status":
                        await HandleStatusUpdateAsync(sessionId, root);
                        break;
                    case "error":
                        await HandleErrorAsync(sessionId, root);
                        break;
                    default:
                        _logger.LogDebug("Unknown message type {MessageType} for session {SessionId}", messageType, sessionId);
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process received message for session {SessionId}: {Message}", sessionId, message);
        }
    }

    private async Task HandleAudioResponseAsync(string sessionId, JsonElement root)
    {
        if (root.TryGetProperty("audio_data", out var audioDataElement) && audioDataElement.ValueKind == JsonValueKind.String)
        {
            var base64Audio = audioDataElement.GetString();
            if (!string.IsNullOrEmpty(base64Audio))
            {
                var audioData = Convert.FromBase64String(base64Audio);
                var timestamp = root.TryGetProperty("timestamp", out var tsElement) ? tsElement.GetInt64() : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var sequenceNumber = root.TryGetProperty("sequence_number", out var seqElement) ? seqElement.GetInt32() : 0;
                
                OnAudioDataReceived(sessionId, audioData, timestamp, sequenceNumber);
            }
        }
        await Task.CompletedTask;
    }

    private async Task HandleTranscriptionAsync(string sessionId, JsonElement root)
    {
        if (root.TryGetProperty("text", out var textElement) && textElement.ValueKind == JsonValueKind.String)
        {
            var text = textElement.GetString() ?? string.Empty;
            var isUser = root.TryGetProperty("is_user", out var isUserElement) && isUserElement.GetBoolean();
            var timestamp = root.TryGetProperty("timestamp", out var tsElement) ? tsElement.GetInt64() : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            
            OnTranscriptionReceived(sessionId, text, isUser, timestamp);
        }
        await Task.CompletedTask;
    }

    private async Task HandleStatusUpdateAsync(string sessionId, JsonElement root)
    {
        if (root.TryGetProperty("status", out var statusElement) && statusElement.ValueKind == JsonValueKind.String)
        {
            var status = statusElement.GetString() ?? string.Empty;
            Dictionary<string, object>? metadata = null;
            
            if (root.TryGetProperty("metadata", out var metadataElement))
            {
                metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(metadataElement.GetRawText());
            }
            
            OnStatusChanged(sessionId, status, metadata);
        }
        await Task.CompletedTask;
    }

    private async Task HandleErrorAsync(string sessionId, JsonElement root)
    {
        var errorCode = root.TryGetProperty("error_code", out var codeElement) ? codeElement.GetString() ?? "UNKNOWN" : "UNKNOWN";
        var errorMessage = root.TryGetProperty("error_message", out var msgElement) ? msgElement.GetString() ?? "Unknown error" : "Unknown error";
        
        OnErrorOccurred(sessionId, errorCode, errorMessage);
        await Task.CompletedTask;
    }

    private void OnAudioDataReceived(string sessionId, byte[] audioData, long timestamp, int sequenceNumber = 0)
    {
        AudioDataReceived?.Invoke(this, new AudioDataReceivedEventArgs
        {
            SessionId = sessionId,
            AudioData = audioData,
            Timestamp = timestamp,
            SequenceNumber = sequenceNumber
        });
    }

    private void OnTranscriptionReceived(string sessionId, string text, bool isUser, long timestamp)
    {
        TranscriptionReceived?.Invoke(this, new TranscriptionReceivedEventArgs
        {
            SessionId = sessionId,
            Text = text,
            IsUser = isUser,
            Timestamp = timestamp
        });
    }

    private void OnStatusChanged(string sessionId, string status, Dictionary<string, object>? metadata = null)
    {
        StatusChanged?.Invoke(this, new StatusChangedEventArgs
        {
            SessionId = sessionId,
            Status = status,
            Metadata = metadata
        });
    }

    private void OnErrorOccurred(string sessionId, string errorCode, string errorMessage, Exception? exception = null)
    {
        ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs
        {
            SessionId = sessionId,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            Exception = exception
        });
    }

    public void Dispose()
    {
        foreach (var kvp in _connections)
        {
            try
            {
                if (kvp.Value.State == WebSocketState.Open)
                {
                    kvp.Value.CloseAsync(WebSocketCloseStatus.NormalClosure, "Service disposing", CancellationToken.None).Wait(TimeSpan.FromSeconds(5));
                }
                kvp.Value.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing WebSocket for session {SessionId}", kvp.Key);
            }
        }
        
        foreach (var kvp in _cancellationTokens)
        {
            kvp.Value.Cancel();
            kvp.Value.Dispose();
        }
        
        _connections.Clear();
        _cancellationTokens.Clear();
    }
}