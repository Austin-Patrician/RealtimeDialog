using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RealtimeDialog.Core.Protocol;
using RealtimeDialog.Core.Audio;

namespace RealtimeDialog.Core.Services;

public class ServerResponseService : IDisposable
{
    private readonly ILogger<ServerResponseService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly WebSocketClient _webSocketClient;
    private readonly AudioPlayer _audioPlayer;
    private readonly AudioService? _audioService; // Optional dependency for state sync
    private readonly ConcurrentQueue<float[]> _audioBuffer = new();
    private readonly object _bufferLock = new();
    private volatile bool _isSendingChatTTSText;
    private volatile bool _isUserQuerying;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    
    // Audio constants
    private const int SampleRate = 24000;
    private const int Channels = 1;
    private const int BufferSeconds = 100;
    private const int MaxBufferSize = SampleRate * BufferSeconds;
    
    public event Action? UserQueryDetected;
    public event Action? UserQueryFinished;
    public event Action<int>? SessionFinished;

    public ServerResponseService(
        ILogger<ServerResponseService> logger,
        ILoggerFactory loggerFactory,
        WebSocketClient webSocketClient,
        AudioPlayer audioPlayer,
        AudioService? audioService = null)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _webSocketClient = webSocketClient;
        _audioPlayer = audioPlayer;
        _audioService = audioService;
    }

    public async Task StartListeningAsync(CancellationToken cancellationToken = default)
    {
        var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _cancellationTokenSource.Token).Token;

        // Initialize and start audio player
        if (!await _audioPlayer.InitializeAsync())
        {
            _logger.LogError("Failed to initialize audio player");
            return;
        }
        
        if (!await _audioPlayer.StartPlaybackAsync(GetNextAudioBuffer, combinedToken))
        {
            _logger.LogError("Failed to start audio playback");
            return;
        }
        
        _logger.LogInformation("Audio player started successfully");

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
            await _audioPlayer.StopPlaybackAsync();
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
                // ASR info event - clear audio buffer
                ClearAudioBuffer();
                UserQueryDetected?.Invoke();
                _isUserQuerying = true;
                // Sync state with AudioService
                if (_audioService != null)
                {
                    _audioService.SetUserQueryingState(true);
                }
                break;
                
            case 350 when _isSendingChatTTSText:
                // Handle ChatTTSText response
                await HandleChatTTSTextResponseAsync(message);
                break;
                
            case 459:
                _isUserQuerying = false;
                UserQueryFinished?.Invoke();
                // Sync state with AudioService
                if (_audioService != null)
                {
                    _audioService.SetUserQueryingState(false);
                }
                
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
                ClearAudioBuffer();
                _isSendingChatTTSText = false;
                // Sync state with AudioService
                if (_audioService != null)
                {
                    _audioService.SetChatTTSTextState(false);
                }
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
            // Sync state with AudioService
            if (_audioService != null)
            {
                _audioService.SetChatTTSTextState(true);
            }
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
            // Sync state with AudioService on error
            if (_audioService != null)
            {
                _audioService.SetChatTTSTextState(false);
            }
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
            // Use payload bytes directly
            var audioBytes = message.Payload ?? Array.Empty<byte>();
            HandleIncomingAudio(audioBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing audio message");
        }
    }

    private void HandleIncomingAudio(byte[] data)
    {
        if (_isSendingChatTTSText)
        {
            _logger.LogDebug("Skipping audio data - currently sending ChatTTS text");
            return;
        }

        _logger.LogDebug("Received audio byte len: {ByteLen}, float32 len: {Float32Len}", 
            data.Length, data.Length / 4);
            
        var sampleCount = data.Length / 4;
        var samples = new float[sampleCount];
        
        // Convert bytes to float32 samples (little-endian)
        for (int i = 0; i < sampleCount; i++)
        {
            var bits = BitConverter.ToUInt32(data, i * 4);
            samples[i] = BitConverter.UInt32BitsToSingle(bits);
        }
        
        _logger.LogDebug("Converted {SampleCount} samples, first few values: [{Sample1}, {Sample2}, {Sample3}]", 
            sampleCount, 
            sampleCount > 0 ? samples[0] : 0f,
            sampleCount > 1 ? samples[1] : 0f,
            sampleCount > 2 ? samples[2] : 0f);

        // Add to audio buffer
        lock (_bufferLock)
        {
            _audioBuffer.Enqueue(samples);
            _logger.LogDebug("Added samples to buffer, current buffer count: {BufferCount}", _audioBuffer.Count);
            
            // Limit buffer size
            var totalSamples = 0;
            var tempQueue = new Queue<float[]>();
            
            while (_audioBuffer.TryDequeue(out var buffer))
            {
                tempQueue.Enqueue(buffer);
                totalSamples += buffer.Length;
            }
            
            // Keep only the most recent samples within buffer limit
            while (totalSamples > MaxBufferSize && tempQueue.Count > 0)
            {
                var oldBuffer = tempQueue.Dequeue();
                totalSamples -= oldBuffer.Length;
            }
            
            // Put remaining buffers back
            while (tempQueue.Count > 0)
            {
                _audioBuffer.Enqueue(tempQueue.Dequeue());
            }
        }
    }

    private float[]? GetNextAudioBuffer()
    {
        lock (_bufferLock)
        {
            var hasBuffer = _audioBuffer.TryDequeue(out var buffer);
            if (hasBuffer && buffer != null)
            {
                _logger.LogTrace("Providing {SampleCount} samples to AudioPlayer, remaining buffer count: {BufferCount}", 
                    buffer.Length, _audioBuffer.Count);
                return buffer;
            }
            else
            {
                _logger.LogTrace("No audio buffer available for AudioPlayer");
                return null;
            }
        }
    }

    private void ClearAudioBuffer()
    {
        lock (_bufferLock)
        {
            while (_audioBuffer.TryDequeue(out _)) { }
        }
        _logger.LogInformation("Audio buffer cleared");
    }

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