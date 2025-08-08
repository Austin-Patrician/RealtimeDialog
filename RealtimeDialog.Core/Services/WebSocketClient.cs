using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.Logging;
using RealtimeDialog.Core.Protocol;

namespace RealtimeDialog.Core.Services;

public class WebSocketClient : IDisposable
{
    private readonly ILogger<WebSocketClient> _logger;
    private readonly BinaryProtocol _protocol;
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private bool _disposed;

    public WebSocketClient(ILogger<WebSocketClient> logger)
    {
        _logger = logger;
        _protocol = new BinaryProtocol();
        _protocol.SetVersion(VersionBits.Version1);
        _protocol.SetHeaderSize(HeaderSizeBits.HeaderSize4);
        _protocol.SetSerialization(SerializationBits.JSON);
        _protocol.SetCompression(CompressionBits.None);
    }

    public async Task<bool> ConnectAsync(Uri uri, Dictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        try
        {
            _webSocket = new ClientWebSocket();
            _cancellationTokenSource = new CancellationTokenSource();

            // Add headers
            foreach (var header in headers)
            {
                _webSocket.Options.SetRequestHeader(header.Key, header.Value);
            }

            await _webSocket.ConnectAsync(uri, cancellationToken);
            _logger.LogInformation("WebSocket connected to {Uri}", uri);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to WebSocket at {Uri}", uri);
            return false;
        }
    }

    public async Task<bool> SendMessageAsync(Message message, CancellationToken cancellationToken = default)
    {
        if (_webSocket?.State != WebSocketState.Open)
        {
            _logger.LogError("WebSocket is not connected");
            return false;
        }

        try
        {
            await _writeLock.WaitAsync(cancellationToken);
            try
            {
                var frame = _protocol.Marshal(message);
                _logger.LogDebug("Sending message frame: {Frame}", Convert.ToHexString(frame.Take(Math.Min(frame.Length, 100)).ToArray()));
                
                await _webSocket.SendAsync(
                    new ArraySegment<byte>(frame),
                    WebSocketMessageType.Binary,
                    true,
                    cancellationToken);
                
                return true;
            }
            finally
            {
                _writeLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message");
            return false;
        }
    }

    public async Task<Message?> ReceiveMessageAsync(CancellationToken cancellationToken = default)
    {
        if (_webSocket?.State != WebSocketState.Open)
        {
            _logger.LogError("WebSocket is not connected");
            return null;
        }

        try
        {
            var buffer = new byte[8192];
            var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            
            if (result.MessageType == WebSocketMessageType.Close)
            {
                _logger.LogInformation("WebSocket connection closed by server");
                return null;
            }

            var frameData = new byte[result.Count];
            Array.Copy(buffer, frameData, result.Count);
            
            var framePrefix = frameData.Take(Math.Min(frameData.Length, 100)).ToArray();
            _logger.LogDebug("Received frame prefix: {Frame}", Convert.ToHexString(framePrefix));
            
            ContainsSequenceFunc containsSequence = (MsgTypeFlagBits bits) => {
                // Check if the message type flag indicates sequence presence
                return Message.ContainsSequence(bits);
            };
            
            var protocolLogger = new Microsoft.Extensions.Logging.LoggerFactory().CreateLogger<BinaryProtocol>();
            var (message, protocol) = BinaryProtocol.Unmarshal(frameData, containsSequence, protocolLogger);
            return message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to receive message");
            return null;
        }
    }

    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (_webSocket?.State == WebSocketState.Open)
        {
            try
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing", cancellationToken);
                _logger.LogInformation("WebSocket connection closed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing WebSocket connection");
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _cancellationTokenSource?.Cancel();
        _webSocket?.Dispose();
        _cancellationTokenSource?.Dispose();
        _writeLock.Dispose();
        _disposed = true;
    }
}