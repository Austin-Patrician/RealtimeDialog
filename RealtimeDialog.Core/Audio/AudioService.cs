using Microsoft.Extensions.Logging;
using RealtimeDialog.Core.Services;
using NAudio.Wave;
using System.Collections.Concurrent;

namespace RealtimeDialog.Core.Audio;

public class AudioService : IDisposable
{
    private readonly ILogger<AudioService> _logger;
    private readonly AudioRecorder _audioRecorder;
    private readonly AudioPlayer _audioPlayer;
    private readonly ClientRequestService _clientRequestService;
    private string _sessionId = string.Empty;
    private bool _disposed;
    private CancellationTokenSource? _cancellationTokenSource;
    
    // NAudio constants matching Go implementation
    private const int InputSampleRate = 16000;  // Input from microphone
    private const int OutputSampleRate = 24000; // Output to speakers
    private const int Channels = 1;
    private const int FramesPerBuffer = 512;
    private const int BufferSeconds = 100;
    
    // Audio buffers
    private readonly ConcurrentQueue<float[]> _audioBuffer = new();
    private readonly object _bufferLock = new();
    private volatile bool _isSendingChatTTSText = false;
    private volatile bool _isUserQuerying = false;

    public AudioService(
        ILogger<AudioService> logger,
        AudioRecorder audioRecorder,
        AudioPlayer audioPlayer,
        ClientRequestService clientRequestService)
    {
        _logger = logger;
        _audioRecorder = audioRecorder;
        _audioPlayer = audioPlayer;
        _clientRequestService = clientRequestService;
        
        // Subscribe to audio data events
        _audioRecorder.AudioDataReceived += OnAudioDataReceived;
    }

    public async Task<bool> InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Initializing NAudio-based audio service...");
            
            var recorderInitialized = await _audioRecorder.InitializeAsync();
            if (!recorderInitialized)
            {
                _logger.LogError("Failed to initialize audio recorder");
                return false;
            }

            var playerInitialized = await _audioPlayer.InitializeAsync();
            if (!playerInitialized)
            {
                _logger.LogError("Failed to initialize audio player");
                return false;
            }

            _logger.LogInformation("NAudio audio service initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during NAudio audio service initialization");
            return false;
        }
    }

    public async Task<bool> StartAudioProcessingAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            _logger.LogError("Session ID cannot be null or empty");
            return false;
        }

        _sessionId = sessionId;
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            _logger.LogInformation("Starting audio processing for session {SessionId}", sessionId);

            // Initialize user querying state to true to allow initial audio data sending
            // This will be managed by ServerResponseService based on server events (450/459)
            _isUserQuerying = true;
            _logger.LogInformation("Initialized user querying state to true - ready to send audio data");

            // Start audio recording
            var recordingStarted = await _audioRecorder.StartRecordingAsync(_cancellationTokenSource.Token);
            if (!recordingStarted)
            {
                _logger.LogError("Failed to start audio recording");
                return false;
            }

            // Start audio playback (will be handled by ServerResponseService)
            _logger.LogInformation("Audio processing started successfully - recording active and ready to send data");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during audio processing start");
            return false;
        }
    }

    public async Task StopAudioProcessingAsync()
    {
        try
        {
            _logger.LogInformation("Stopping audio processing...");

            _cancellationTokenSource?.Cancel();

            await _audioRecorder.StopRecordingAsync();
            await _audioPlayer.StopPlaybackAsync();

            _logger.LogInformation("Audio processing stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during audio processing stop");
        }
    }

    private async void OnAudioDataReceived(short[] audioData)
    {
        try
        {
            _logger.LogDebug($"Received audio data: {audioData.Length} samples, isUserQuerying: {_isUserQuerying}");
            
            if (string.IsNullOrEmpty(_sessionId))
            {
                _logger.LogWarning("Received audio data but session ID is not set");
                return;
            }

            if (_cancellationTokenSource?.Token.IsCancellationRequested == true)
            {
                _logger.LogDebug("Cancellation requested, skipping audio data");
                return;
            }

            // Skip sending audio if user is not actively querying
            if (!_isUserQuerying)
            {
                return;
            }

            // Convert int16 audio data to PCM bytes (S16LE format)
            var audioBytes = AudioRecorder.ConvertInt16ToPCM(audioData);
            _logger.LogDebug($"Converted {audioData.Length} samples to {audioBytes.Length} bytes");
            
            // Send audio data to server
            var success = await _clientRequestService.SendAudioAsync(_sessionId, audioBytes, _cancellationTokenSource?.Token ?? CancellationToken.None);
            if (!success)
            {
                _logger.LogWarning("Failed to send audio data to server");
            }
            else
            {
                _logger.LogDebug($"Successfully sent {audioBytes.Length} bytes of audio data to server");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling audio data");
        }
    }

    /// <summary>
    /// Clear audio buffer - called when receiving specific server events
    /// </summary>
    public void ClearAudioBuffer()
    {
        lock (_bufferLock)
        {
            while (_audioBuffer.TryDequeue(out _)) { }
            _logger.LogDebug("Audio buffer cleared");
        }
    }
    
    /// <summary>
    /// Add audio data to playback buffer
    /// </summary>
    public void AddAudioToBuffer(byte[] audioData)
    {
        try
        {
            // Convert bytes to float samples for NAudio
            var samples = new float[audioData.Length / 2];
            for (int i = 0; i < samples.Length; i++)
            {
                var sample = BitConverter.ToInt16(audioData, i * 2);
                samples[i] = sample / 32768.0f; // Convert to float [-1, 1]
            }
            
            lock (_bufferLock)
            {
                _audioBuffer.Enqueue(samples);
            }
            
            _logger.LogDebug($"Added {samples.Length} audio samples to buffer");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception adding audio to buffer");
        }
    }
    
    /// <summary>
    /// Set user querying state (matching Go's isUserQuerying atomic operation)
    /// </summary>
    public void SetUserQueryingState(bool isQuerying)
    {
        _isUserQuerying = isQuerying;
        _logger.LogDebug($"User querying state: {isQuerying}");
    }
    
    /// <summary>
    /// Set ChatTTS text sending state (matching Go's state management)
    /// </summary>
    public void SetChatTTSTextState(bool isSending)
    {
        _isSendingChatTTSText = isSending;
        _logger.LogDebug($"Sending ChatTTS text state: {isSending}");
    }
    
    /// <summary>
    /// Get user querying state (for compatibility)
    /// </summary>
    public bool IsUserQuerying => _isUserQuerying;
    
    /// <summary>
    /// Get ChatTTS text sending state (for compatibility)
    /// </summary>
    public bool IsSendingChatTTSText => _isSendingChatTTSText;
    
    /// <summary>
    /// Get next audio samples from buffer for playback
    /// </summary>
    public float[]? GetNextAudioSamples()
    {
        lock (_bufferLock)
        {
            return _audioBuffer.TryDequeue(out var samples) ? samples : null;
        }
    }

    public int GetAudioBufferSize()
    {
        lock (_bufferLock)
        {
            return _audioBuffer.Count;
        }
    }

    public bool IsRecording => _audioRecorder != null && !_disposed;
    public bool IsPlaying => _audioPlayer != null && !_disposed;

    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            StopAudioProcessingAsync().Wait(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during audio service disposal");
        }

        _audioRecorder?.Dispose();
        _audioPlayer?.Dispose();
        _cancellationTokenSource?.Dispose();
        
        // Clear audio buffer
        ClearAudioBuffer();
        
        _disposed = true;
        _logger.LogInformation("NAudio audio service disposed");
    }
}