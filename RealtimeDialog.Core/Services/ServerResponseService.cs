using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RealtimeDialog.Core.Protocol;

namespace RealtimeDialog.Core.Services;

public class ServerResponseService : IDisposable
{
    private readonly ILogger<ServerResponseService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly WebSocketClient _webSocketClient;
    // Audio processing moved to frontend - no longer needed
    private volatile bool _isSendingChatTTSText;
    private volatile bool _isUserQuerying;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    
    // Audio processing moved to frontend
    
    public event Action? UserQueryDetected;
    public event Action? UserQueryFinished;
    public event Action<int>? SessionFinished;
    public event Action<byte[]>? AudioDataReceived;

    public ServerResponseService(
        ILogger<ServerResponseService> logger,
        ILoggerFactory loggerFactory,
        WebSocketClient webSocketClient)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _webSocketClient = webSocketClient;
    }

    public async Task StartListeningAsync(CancellationToken cancellationToken = default)
    {
        var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _cancellationTokenSource.Token).Token;

        // Audio playback handled by frontend
        _logger.LogInformation("Server response service started - audio handled by frontend");

        try
        {
            while (!combinedToken.IsCancellationRequested)
            {
                _logger.LogInformation("Waiting for message...");
                var message = await _webSocketClient.ReceiveMessageAsync(combinedToken);
                
                if (message == null)
                {
                    _logger.LogWarning("Received null message, connection may be closed");
                    break;
                }

                await ProcessMessageAsync(message, combinedToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Message listening cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in message listening loop");
        }
        finally
        {
            // Audio cleanup handled by frontend
            _logger.LogInformation("Server response service stopped");
        }
    }

    private async Task ProcessMessageAsync(Message message, CancellationToken cancellationToken)
    {
        switch (message.Type)
        {
            case MsgType.FullServer:
                await ProcessTextMessageAsync(message, cancellationToken);
                break;
                
            case MsgType.AudioOnlyServer:
                ProcessAudioMessage(message);
                break;
                
            case MsgType.Error:
                _logger.LogError("Received Error message (code={ErrorCode}): {Payload}", 
                    message.ErrorCode, message.Payload);
                break;
                
            default:
                _logger.LogError("Received unexpected message type: {Type}", message.Type);
                break;
        }
    }

    private async Task ProcessTextMessageAsync(Message message, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received text message (event={Event}, session_id={SessionId}): {Payload}", 
            message.Event, message.SessionID, message.Payload);

        switch (message.Event)
        {
            case 152:
            case 153:
                // Session finished events
                _logger.LogInformation("Session finished with event {Event}", message.Event);
                SessionFinished?.Invoke(message.Event);
                return;
                
            case 450:
                // ASR info event - user query detected
                UserQueryDetected?.Invoke();
                _isUserQuerying = true;
                break;
                
            case 350 when _isSendingChatTTSText:
                // Handle ChatTTSText response
                await HandleChatTTSTextResponseAsync(message);
                break;
                
            case 459:
                _isUserQuerying = false;
                UserQueryFinished?.Invoke();
                
                // Randomly trigger ChatTTSText request (50% chance)
                if (Random.Shared.Next(2) == 0)
                {
                    _ = Task.Run(() => SendChatTTSTextSequenceAsync(message.SessionID ?? "", cancellationToken), cancellationToken);
                }
                break;
        }
    }

    private async Task HandleChatTTSTextResponseAsync(Message message)
    {
        try
        {
            var payloadString = Encoding.UTF8.GetString(message.Payload ?? Array.Empty<byte>());
            if (string.IsNullOrEmpty(payloadString))
                return;
                
            var jsonData = JsonSerializer.Deserialize<Dictionary<string, object>>(payloadString);
            if (jsonData?.TryGetValue("tts_type", out var ttsTypeObj) == true && 
                ttsTypeObj.ToString() == "chat_tts_text")
            {
                _isSendingChatTTSText = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling ChatTTSText response");
        }
    }

    private async Task SendChatTTSTextSequenceAsync(string sessionId, CancellationToken cancellationToken)
    {
        try
        {
            _isSendingChatTTSText = true;
            _logger.LogInformation("Hit ChatTTSText event, start sending...");
            
            var clientRequestService = new ClientRequestService(_loggerFactory.CreateLogger<ClientRequestService>(), _webSocketClient);
            
            // First round
            await clientRequestService.ChatTTSTextAsync(sessionId, new ChatTTSTextPayload
            {
                Start = true,
                End = false,
                Content = "这是第一轮TTS的开始和中间包事件，这两个合而为一了。"
            }, cancellationToken);
            
            await clientRequestService.ChatTTSTextAsync(sessionId, new ChatTTSTextPayload
            {
                Start = false,
                End = true,
                Content = "这是第一轮TTS的结束事件。"
            }, cancellationToken);
            
            // Wait 10 seconds
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            
            // Second round
            await clientRequestService.ChatTTSTextAsync(sessionId, new ChatTTSTextPayload
            {
                Start = true,
                End = false,
                Content = "这是第二轮TTS的开始和中间包事件，这两个合而为一了。"
            }, cancellationToken);
            
            await clientRequestService.ChatTTSTextAsync(sessionId, new ChatTTSTextPayload
            {
                Start = false,
                End = true,
                Content = "这是第二轮TTS的结束事件。"
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending ChatTTSText sequence");
            _isSendingChatTTSText = false;
        }
    }

    private void ProcessAudioMessage(Message message)
    {
        _logger.LogInformation("Received audio message (event={Event}): session_id={SessionId}", 
            message.Event, message.SessionID);
            
        if (_isSendingChatTTSText)
        {
            return;
        }

        try
        {
            // Forward audio data to frontend via event
            var audioBytes = message.Payload ?? Array.Empty<byte>();
            AudioDataReceived?.Invoke(audioBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing audio message");
        }
    }

    // Audio buffer management moved to frontend

    public void Stop()
    {
        _cancellationTokenSource.Cancel();
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}