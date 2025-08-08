using Microsoft.Extensions.Logging;
using NAudio.Wave;
using System.Collections.Concurrent;

namespace RealtimeDialog.Core.Audio;

public class AudioPlayer : IDisposable
{
    private readonly ILogger<AudioPlayer> _logger;
    private bool _isPlaying;
    private bool _disposed;
    private CancellationTokenSource? _playbackCancellation;
    
    // NAudio components
    private WaveOutEvent? _waveOut;
    private BufferedWaveProvider? _waveProvider;
    
    // Audio constants matching Go implementation
    private const int SampleRate = 24000;
    private const int Channels = 1;
    private const int FramesPerBuffer = 512;
    private const int BitsPerSample = 32; // 32-bit float format
    
    // Playback synchronization and callback provider
    private readonly object _playbackLock = new();
    private Func<float[]?>? _audioBufferProvider;

    public bool IsPlaying => _isPlaying;

    public AudioPlayer(ILogger<AudioPlayer> logger)
    {
        _logger = logger;
    }

    public async Task<bool> InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Initializing NAudio player...");
            
            // Create wave format (32-bit IEEE float, 24kHz, mono) - matching Go's float32 format
            var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, Channels);
            
            // Create buffered wave provider
            _waveProvider = new BufferedWaveProvider(waveFormat)
            {
                BufferLength = SampleRate * 4 * 10, // 10 seconds buffer (4 bytes per float32 sample)
                DiscardOnBufferOverflow = true
            };
            
            // Create wave output device
            _waveOut = new WaveOutEvent
            {
                DesiredLatency = 10, // 10ms latency matching Go implementation
                NumberOfBuffers = 3
            };
            
            _waveOut.Init(_waveProvider);
            
            _logger.LogInformation("NAudio player initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during NAudio player initialization");
            return false;
        }
    }

    public async Task<bool> StartPlaybackAsync(Func<float[]?> audioBufferProvider, CancellationToken cancellationToken = default)
    {
        if (_isPlaying)
        {
            _logger.LogWarning("Audio playback is already running");
            return false;
        }

        if (audioBufferProvider == null)
        {
            _logger.LogError("Audio buffer provider is null");
            return false;
        }

        try
        {
            if (_waveOut == null || _waveProvider == null)
            {
                _logger.LogError("Audio player not initialized");
                return false;
            }
            
            lock (_playbackLock)
            {
                _audioBufferProvider = audioBufferProvider;
                _playbackCancellation = new CancellationTokenSource();
                _isPlaying = true;
            }
            
            // Start NAudio playback - similar to Go's PortAudio stream start
            _waveOut.Play();
            
            _logger.LogInformation("NAudio playback started with direct callback mechanism");
            
            // Start real-time audio feeding task (matching Go's callback approach)
            _ = Task.Run(async () => await FeedAudioContinuouslyAsync(_playbackCancellation.Token), _playbackCancellation.Token);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during NAudio playback start");
            return false;
        }
    }

    public async Task StopPlaybackAsync()
    {
        if (!_isPlaying)
        {
            return;
        }

        try
        {
            lock (_playbackLock)
            {
                _playbackCancellation?.Cancel();
                _playbackCancellation?.Dispose();
                _playbackCancellation = null;
                _audioBufferProvider = null;
                _isPlaying = false;
            }
            
            // Stop NAudio playback
            _waveOut?.Stop();
            
            _logger.LogInformation("NAudio playback stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during NAudio playback stop");
        }
    }

    private async Task FeedAudioContinuouslyAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_waveProvider == null)
                    break;
                    
                // Get audio data directly from provider (matching Go's callback approach)
                Func<float[]?>? provider;
                lock (_playbackLock)
                {
                    provider = _audioBufferProvider;
                }
                
                var audioData = provider?.Invoke();
                
                if (audioData != null && audioData.Length > 0)
                {
                    // Convert and feed directly to NAudio (no internal buffering)
                    FeedSamplesToNAudio(audioData);
                    _logger.LogTrace("Fed {SampleCount} samples directly to NAudio", audioData.Length);
                }
                
                // Wait for next processing cycle (matching Go's 10ms latency)
                await Task.Delay(10, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in continuous audio feeding");
        }
    }
    
    private void FeedSamplesToNAudio(float[] samples)
    {
        if (_waveProvider == null || samples == null || samples.Length == 0) 
            return;
        
        // Convert float samples to bytes for NAudio (32-bit IEEE float format)
        // Direct conversion matching Go's float32 handling - no internal buffering
        var bytes = new byte[samples.Length * 4]; // 4 bytes per float32 sample
        for (int i = 0; i < samples.Length; i++)
        {
            // Clamp samples to valid range [-1.0, 1.0] but keep as float32
            var sample = Math.Max(-1.0f, Math.Min(1.0f, samples[i]));
            var sampleBytes = BitConverter.GetBytes(sample);
            bytes[i * 4] = sampleBytes[0];
            bytes[i * 4 + 1] = sampleBytes[1];
            bytes[i * 4 + 2] = sampleBytes[2];
            bytes[i * 4 + 3] = sampleBytes[3];
        }
        
        // Add directly to wave provider (matching Go's direct output approach)
        _waveProvider.AddSamples(bytes, 0, bytes.Length);
    }



    /// <summary>
    /// Clear wave provider buffer - matching Go implementation
    /// </summary>
    public void ClearBuffer()
    {
        _waveProvider?.ClearBuffer();
        _logger.LogDebug("Audio playback buffer cleared");
    }

    public void Dispose()
    {
        if (_disposed) return;

        if (_isPlaying)
        {
            StopPlaybackAsync().Wait(TimeSpan.FromSeconds(5));
        }

        // Dispose NAudio components
        _waveOut?.Dispose();
        _waveProvider = null;
        
        lock (_playbackLock)
        {
            _playbackCancellation?.Dispose();
            _audioBufferProvider = null;
        }
        
        _disposed = true;
        _logger.LogInformation("NAudio player disposed");
    }
}