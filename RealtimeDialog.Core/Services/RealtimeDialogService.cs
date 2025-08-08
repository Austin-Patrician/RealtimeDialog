using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RealtimeDialog.Core.Audio;
using RealtimeDialog.Core.Configuration;
using System.Collections.Concurrent;

namespace RealtimeDialog.Core.Services;

public class RealtimeDialogService : IDisposable
{
    private readonly ILogger<RealtimeDialogService> _logger;
    private readonly RealtimeDialogConfig _config;
    private readonly WebSocketClient _webSocketClient;
    private readonly ClientRequestService _clientRequestService;
    private readonly ServerResponseService _serverResponseService;
    private readonly AudioService _audioService;
    
    private string _sessionId = string.Empty;
    private string _dialogId = string.Empty;
    private bool _isConnected;
    private bool _isSessionActive;
    private bool _disposed;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly ConcurrentQueue<string> _querySignals = new();
    private Timer? _greetingTimer;

    public event Action? ConnectionEstablished;
    public event Action? ConnectionLost;
    public event Action<string>? SessionStarted;
    public event Action? SessionEnded;
    public event Action? UserQueryDetected;
    public event Action? UserQueryFinished;

    public RealtimeDialogService(
        ILogger<RealtimeDialogService> logger,
        IOptions<RealtimeDialogConfig> config,
        WebSocketClient webSocketClient,
        ClientRequestService clientRequestService,
        ServerResponseService serverResponseService,
        AudioService audioService)
    {
        _logger = logger;
        _config = config.Value;
        _webSocketClient = webSocketClient;
        _clientRequestService = clientRequestService;
        _serverResponseService = serverResponseService;
        _audioService = audioService;
        
        // Subscribe to server response events
        _serverResponseService.UserQueryDetected += OnUserQueryDetected;
        _serverResponseService.UserQueryFinished += OnUserQueryFinished;
        _serverResponseService.SessionFinished += OnSessionFinished;
    }

    public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isConnected)
        {
            _logger.LogWarning("Service is already running");
            return true;
        }

        try
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _sessionId = Guid.NewGuid().ToString();
            
            _logger.LogInformation("Starting RealtimeDialog service with session {SessionId}", _sessionId);

            // Initialize audio service
            if (!await _audioService.InitializeAsync())
            {
                _logger.LogError("Failed to initialize audio service");
                return false;
            }

            // Connect to WebSocket
            var uri = new Uri(_config.WebSocket.Url);
            var headers = new Dictionary<string, string>
            {
                { "X-Api-Resource-Id", _config.WebSocket.ResourceId },
                { "X-Api-Access-Key", _config.WebSocket.AccessToken },
                { "X-Api-App-Key", _config.WebSocket.AppKey },
                { "X-Api-App-ID", _config.WebSocket.AppId },
                { "X-Api-Connect-Id", Guid.NewGuid().ToString() }
            };

            if (!await _webSocketClient.ConnectAsync(uri, headers, _cancellationTokenSource.Token))
            {
                _logger.LogError("Failed to connect to WebSocket");
                return false;
            }

            // Start connection
            if (!await _clientRequestService.StartConnectionAsync(_cancellationTokenSource.Token))
            {
                _logger.LogError("Failed to start connection");
                return false;
            }

            _isConnected = true;
            ConnectionEstablished?.Invoke();

            // Start session
            var sessionPayload = new StartSessionPayload
            {
                TTS = new TTSPayload
                {
                    AudioConfig = new AudioConfig
                    {
                        Channel = _config.Audio.Output.Channels,
                        Format = "pcm",
                        SampleRate = _config.Audio.Output.SampleRate
                    }
                },
                Dialog = new DialogPayload
                {
                    BotName = _config.Dialog.BotName,
                    SystemRole = _config.Dialog.SystemRole,
                    SpeakingStyle = _config.Dialog.SpeakingStyle,
                    Extra = _config.Dialog.Extra
                }
            };

            _dialogId = await _clientRequestService.StartSessionAsync(_sessionId, sessionPayload, _cancellationTokenSource.Token) ?? "";
            if (string.IsNullOrEmpty(_dialogId))
            {
                _logger.LogError("Failed to start session");
                return false;
            }

            _isSessionActive = true;
            SessionStarted?.Invoke(_dialogId);

            // Send initial greeting
            await _clientRequestService.SayHelloAsync(_sessionId, new SayHelloPayload
            {
                Content = _config.Dialog.DefaultGreeting
            }, _cancellationTokenSource.Token);

            // Start audio processing
            if (!await _audioService.StartAudioProcessingAsync(_sessionId, _cancellationTokenSource.Token))
            {
                _logger.LogError("Failed to start audio processing");
                return false;
            }

            // Start server response listening
            _ = Task.Run(() => _serverResponseService.StartListeningAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);

            // Start greeting timer
            StartGreetingTimer();

            _logger.LogInformation("RealtimeDialog service started successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start RealtimeDialog service");
            await StopAsync();
            return false;
        }
    }

    public async Task StopAsync()
    {
        if (!_isConnected && !_isSessionActive)
        {
            return;
        }

        try
        {
            _logger.LogInformation("Stopping RealtimeDialog service...");

            _cancellationTokenSource?.Cancel();
            _greetingTimer?.Dispose();

            // Stop audio processing
            await _audioService.StopAudioProcessingAsync();

            // Finish session if active
            if (_isSessionActive && !string.IsNullOrEmpty(_sessionId))
            {
                await _clientRequestService.FinishSessionAsync(_sessionId);
                _isSessionActive = false;
                SessionEnded?.Invoke();
            }

            // Finish connection if connected
            if (_isConnected)
            {
                await _clientRequestService.FinishConnectionAsync();
                await _webSocketClient.CloseAsync();
                _isConnected = false;
                ConnectionLost?.Invoke();
            }

            _logger.LogInformation("RealtimeDialog service stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping RealtimeDialog service");
        }
    }

    private void StartGreetingTimer()
    {
        _greetingTimer?.Dispose();
        _greetingTimer = new Timer(async _ =>
        {
            try
            {
                if (_isSessionActive && !string.IsNullOrEmpty(_sessionId))
                {
                    _logger.LogInformation("Timeout waiting for user query, sending greeting...");
                    await _clientRequestService.SayHelloAsync(_sessionId, new SayHelloPayload
                    {
                        Content = _config.Dialog.TimeoutGreeting
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending timeout greeting");
            }
        }, null, TimeSpan.FromSeconds(_config.Dialog.GreetingTimeoutSeconds), TimeSpan.FromSeconds(_config.Dialog.GreetingTimeoutSeconds));
    }

    private void OnUserQueryDetected()
    {
        _querySignals.Enqueue("query_detected");
        _greetingTimer?.Dispose(); // Stop greeting timer when user starts speaking
        UserQueryDetected?.Invoke();
        _logger.LogInformation("User query detected");
    }

    private void OnUserQueryFinished()
    {
        StartGreetingTimer(); // Restart greeting timer
        UserQueryFinished?.Invoke();
        _logger.LogInformation("User query finished");
    }

    private void OnSessionFinished(int eventCode)
    {
        _logger.LogInformation("Session finished with event code {EventCode}", eventCode);
        _isSessionActive = false;
        SessionEnded?.Invoke();
    }

    public bool IsConnected => _isConnected;
    public bool IsSessionActive => _isSessionActive;
    public string SessionId => _sessionId;
    public string DialogId => _dialogId;

    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            StopAsync().Wait(TimeSpan.FromSeconds(10));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during service disposal");
        }

        _greetingTimer?.Dispose();
        _cancellationTokenSource?.Dispose();
        _audioService?.Dispose();
        _webSocketClient?.Dispose();
        
        _disposed = true;
    }
}