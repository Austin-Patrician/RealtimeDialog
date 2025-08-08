using Microsoft.Extensions.Logging;
using NAudio.Wave;

namespace RealtimeDialog.Core.Audio;

public class AudioRecorder : IDisposable
{
    private readonly ILogger<AudioRecorder> _logger;
    private bool _isRecording;
    private bool _disposed;
    private CancellationTokenSource? _recordingCancellation;
    
    // NAudio components
    private WaveInEvent? _waveIn;
    
    public event Action<short[]>? AudioDataReceived;

    // Audio constants matching Go implementation
    private const int SampleRate = 16000;  // Input sample rate
    private const int Channels = 1;
    private const int FramesPerBuffer = 160;
    private const int BitsPerSample = 16;

    public bool IsRecording => _isRecording;

    public AudioRecorder(ILogger<AudioRecorder> logger)
    {
        _logger = logger;
    }

    public async Task<bool> InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Initializing NAudio recorder...");
            
            // Create wave format (16-bit PCM, 16kHz, mono)
            var waveFormat = new WaveFormat(SampleRate, BitsPerSample, Channels);
            
            // Create wave input device
            _waveIn = new WaveInEvent
            {
                WaveFormat = waveFormat,
                BufferMilliseconds = 10, // 10ms buffer matching Go implementation
                NumberOfBuffers = 3
            };
            
            // Subscribe to data available event
            _waveIn.DataAvailable += OnWaveInDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;
            
            _logger.LogInformation("NAudio recorder initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during NAudio recorder initialization");
            return false;
        }
    }

    public async Task<bool> StartRecordingAsync(CancellationToken cancellationToken = default)
    {
        if (_isRecording)
        {
            _logger.LogWarning("Recording is already in progress");
            return true;
        }

        try
        {
            if (_waveIn == null)
            {
                _logger.LogError("Audio recorder not initialized");
                return false;
            }
            
            _recordingCancellation = new CancellationTokenSource();
            _isRecording = true;
            
            // Start NAudio recording
            _waveIn.StartRecording();
            
            _logger.LogInformation("NAudio recording started");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during NAudio recording start");
            return false;
        }
    }

    public async Task StopRecordingAsync()
    {
        if (!_isRecording)
        {
            return;
        }

        try
        {
            // Stop NAudio recording
            _waveIn?.StopRecording();
            
            _recordingCancellation?.Cancel();
            _recordingCancellation?.Dispose();
            _recordingCancellation = null;
            
            _isRecording = false;
            _logger.LogInformation("NAudio recording stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during NAudio recording stop");
        }
    }

    /// <summary>
    /// Handle NAudio data available event
    /// </summary>
    private void OnWaveInDataAvailable(object? sender, WaveInEventArgs e)
    {
        try
        {
            if (e.BytesRecorded == 0 || !_isRecording)
                return;
                
            // Convert bytes to short samples
            var samples = new short[e.BytesRecorded / 2];
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = BitConverter.ToInt16(e.Buffer, i * 2);
            }
            
            // Raise event with audio data
            AudioDataReceived?.Invoke(samples);
            
            _logger.LogTrace($"Captured {samples.Length} audio samples");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception processing audio data");
        }
    }
    
    /// <summary>
    /// Handle recording stopped event
    /// </summary>
    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception != null)
        {
            _logger.LogError(e.Exception, "Recording stopped due to exception");
        }
        else
        {
            _logger.LogDebug("Recording stopped normally");
        }
    }

    public static byte[] ConvertInt16ToPCM(short[] samples)
    {
        var audioBytes = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            var bytes = BitConverter.GetBytes(samples[i]);
            audioBytes[i * 2] = bytes[0];
            audioBytes[i * 2 + 1] = bytes[1];
        }
        return audioBytes;
    }

    public void Dispose()
    {
        if (_disposed) return;

        if (_isRecording)
        {
            StopRecordingAsync().Wait(TimeSpan.FromSeconds(5));
        }

        // Dispose NAudio components
        if (_waveIn != null)
        {
            _waveIn.DataAvailable -= OnWaveInDataAvailable;
            _waveIn.RecordingStopped -= OnRecordingStopped;
            _waveIn.Dispose();
            _waveIn = null;
        }

        _recordingCancellation?.Dispose();
        _disposed = true;
        _logger.LogInformation("NAudio recorder disposed");
    }
}