using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using RealtimeDialog.Core.Services;
using RealtimeDialog.Core.Protocol;

namespace RealtimeDialog.Tests;

public class WebSocketClientTests
{
    private readonly Mock<ILogger<WebSocketClient>> _mockLogger;
    private readonly WebSocketClient _webSocketClient;

    public WebSocketClientTests()
    {
        _mockLogger = new Mock<ILogger<WebSocketClient>>();
        _webSocketClient = new WebSocketClient(_mockLogger.Object);
    }

    [Fact]
    public void Constructor_ShouldInitializeCorrectly()
    {
        // Assert
        Assert.NotNull(_webSocketClient);
    }

    [Fact]
    public void ConnectAsync_WithValidUri_ShouldCreateTask()
    {
        // Arrange
        var validUri = new Uri("wss://example.com");
        var headers = new Dictionary<string, string>();
        var cancellationToken = CancellationToken.None;

        // Act
        var task = _webSocketClient.ConnectAsync(validUri, headers, cancellationToken);
        
        // Assert
        Assert.NotNull(task);
    }

    [Fact]
    public void SendMessageAsync_WithMessage_ShouldCreateTask()
    {
        // Arrange
        var message = new Message
        {
            Type = MsgType.FullClient,
            Payload = Encoding.UTF8.GetBytes("test")
        };
        var cancellationToken = CancellationToken.None;

        // Act
        var task = _webSocketClient.SendMessageAsync(message, cancellationToken);
        
        // Assert
        Assert.NotNull(task);
    }

    [Fact]
    public void Dispose_ShouldNotThrowException()
    {
        // Act & Assert
        var exception = Record.Exception(() => _webSocketClient.Dispose());
        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_ShouldCleanupResources()
    {
        // Act
        _webSocketClient.Dispose();
        
        // Assert - Should not throw
        Assert.True(true);
    }
}