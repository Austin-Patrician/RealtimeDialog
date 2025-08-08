using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RealtimeDialog.Core.Protocol;

namespace RealtimeDialog.Core.Services;

public class ClientRequestService
{
    private readonly ILogger<ClientRequestService> _logger;
    private readonly WebSocketClient _webSocketClient;

    public ClientRequestService(ILogger<ClientRequestService> logger, WebSocketClient webSocketClient)
    {
        _logger = logger;
        _webSocketClient = webSocketClient;
    }

    public async Task<bool> StartConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var message = Message.CreateMessage(MsgType.FullClient, MsgTypeFlagBits.WithEvent);
            message.Event = 1;
            message.Payload = Encoding.UTF8.GetBytes("{}"); // Empty JSON payload

            _logger.LogInformation("Sending StartConnection request");
            if (!await _webSocketClient.SendMessageAsync(message, cancellationToken))
            {
                return false;
            }

            // Read ConnectionStarted response
            var response = await _webSocketClient.ReceiveMessageAsync(cancellationToken);
            if (response == null)
            {
                _logger.LogError("Failed to receive ConnectionStarted response");
                return false;
            }

            if (response.Type != MsgType.FullServer)
            {
                _logger.LogError("Unexpected ConnectionStarted message type: {Type}", response.Type);
                return false;
            }

            if (response.Event != 50)
            {
                _logger.LogError("Unexpected response event ({Event}) for StartConnection request", response.Event);
                return false;
            }

            _logger.LogInformation("Connection started (event={Event}) connectID: {ConnectID}, payload: {Payload}", 
                response.Event, response.ConnectID, response.Payload);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start connection");
            return false;
        }
    }

    public async Task<string?> StartSessionAsync(string sessionId, StartSessionPayload payload, CancellationToken cancellationToken = default)
    {
        try
        {
            var payloadJson = JsonSerializer.Serialize(payload);
            var message = Message.CreateMessage(MsgType.FullClient, MsgTypeFlagBits.WithEvent);
            message.Event = 100;
            message.SessionID = sessionId;
            message.Payload = Encoding.UTF8.GetBytes(payloadJson);

            _logger.LogInformation("Sending StartSession request for session {SessionId}", sessionId);
            if (!await _webSocketClient.SendMessageAsync(message, cancellationToken))
            {
                return null;
            }

            // Read SessionStarted response
            var response = await _webSocketClient.ReceiveMessageAsync(cancellationToken);
            if (response == null)
            {
                _logger.LogError("Failed to receive SessionStarted response");
                return null;
            }

            if (response.Type != MsgType.FullServer)
            {
                _logger.LogError("Unexpected SessionStarted message type: {Type}", response.Type);
                return null;
            }

            if (response.Event != 150)
            {
                _logger.LogError("Unexpected response event ({Event}) for StartSession request", response.Event);
                return null;
            }

            var payloadString = Encoding.UTF8.GetString(response.Payload ?? Array.Empty<byte>());
            _logger.LogInformation("SessionStarted response payload: {Payload}", payloadString);
            
            // Parse dialog_id from response
            var jsonData = JsonSerializer.Deserialize<Dictionary<string, object>>(payloadString);
            if (jsonData?.TryGetValue("dialog_id", out var dialogIdObj) == true)
            {
                return dialogIdObj.ToString();
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start session");
            return null;
        }
    }

    public async Task<bool> SayHelloAsync(string sessionId, SayHelloPayload payload, CancellationToken cancellationToken = default)
    {
        try
        {
            var payloadJson = JsonSerializer.Serialize(payload);
            _logger.LogInformation("SayHello request payload: {Payload}", payloadJson);
            
            var message = Message.CreateMessage(MsgType.FullClient, MsgTypeFlagBits.WithEvent);
            message.Event = 300;
            message.SessionID = sessionId;
            message.Payload = Encoding.UTF8.GetBytes(payloadJson);

            return await _webSocketClient.SendMessageAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SayHello request");
            return false;
        }
    }

    public async Task<bool> ChatTTSTextAsync(string sessionId, ChatTTSTextPayload payload, CancellationToken cancellationToken = default)
    {
        try
        {
            var payloadJson = JsonSerializer.Serialize(payload);
            _logger.LogInformation("ChatTTSText request payload: {Payload}", payloadJson);
            
            var message = Message.CreateMessage(MsgType.FullClient, MsgTypeFlagBits.WithEvent);
            message.Event = 500;
            message.SessionID = sessionId;
            message.Payload = Encoding.UTF8.GetBytes(payloadJson);

            return await _webSocketClient.SendMessageAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send ChatTTSText request");
            return false;
        }
    }

    public async Task<bool> SendAudioAsync(string sessionId, byte[] audioData, CancellationToken cancellationToken = default)
    {
        try
        {
            var message = Message.CreateMessage(MsgType.AudioOnlyClient, MsgTypeFlagBits.WithEvent);
            message.Event = 200;
            message.SessionID = sessionId;
            message.Payload = audioData;

            return await _webSocketClient.SendMessageAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send audio data");
            return false;
        }
    }

    public async Task<bool> FinishSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var message = Message.CreateMessage(MsgType.FullClient, MsgTypeFlagBits.WithEvent);
            message.Event = 102;
            message.SessionID = sessionId;
            message.Payload = Encoding.UTF8.GetBytes("{}");

            _logger.LogInformation("Sending FinishSession request");
            return await _webSocketClient.SendMessageAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to finish session");
            return false;
        }
    }

    public async Task<bool> FinishConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var message = Message.CreateMessage(MsgType.FullClient, MsgTypeFlagBits.WithEvent);
            message.Event = 2;
            message.Payload = Encoding.UTF8.GetBytes("{}");

            _logger.LogInformation("Sending FinishConnection request");
            if (!await _webSocketClient.SendMessageAsync(message, cancellationToken))
            {
                return false;
            }

            // Read ConnectionFinished response
            var response = await _webSocketClient.ReceiveMessageAsync(cancellationToken);
            if (response == null)
            {
                _logger.LogError("Failed to receive ConnectionFinished response");
                return false;
            }

            if (response.Type != MsgType.FullServer)
            {
                _logger.LogError("Unexpected ConnectionFinished message type: {Type}", response.Type);
                return false;
            }

            if (response.Event != 52)
            {
                _logger.LogError("Unexpected response event ({Event}) for FinishConnection request", response.Event);
                return false;
            }

            _logger.LogInformation("Connection finished (event={Event})", response.Event);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to finish connection");
            return false;
        }
    }
}

// Payload classes
public class StartSessionPayload
{
    public TTSPayload TTS { get; set; } = new();
    public DialogPayload Dialog { get; set; } = new();
}

public class TTSPayload
{
    public TTSAudioConfig AudioConfig { get; set; } = new();
}

public class TTSAudioConfig
{
    public int Channel { get; set; } = 1;
    public string Format { get; set; } = "pcm";
    public int SampleRate { get; set; } = 24000;
}

public class DialogPayload
{
    public string DialogID { get; set; } = string.Empty;
    public string BotName { get; set; } = string.Empty;
    public string SystemRole { get; set; } = string.Empty;
    public string SpeakingStyle { get; set; } = string.Empty;
    public Dictionary<string, object> Extra { get; set; } = new();
}

public class SayHelloPayload
{
    public string Content { get; set; } = string.Empty;
}

public class ChatTTSTextPayload
{
    public bool Start { get; set; }
    public bool End { get; set; }
    public string Content { get; set; } = string.Empty;
}