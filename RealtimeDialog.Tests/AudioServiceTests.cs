using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using RealtimeDialog.Core.Audio;
using RealtimeDialog.Core.Services;
using RealtimeDialog.Core.Configuration;

namespace RealtimeDialog.Tests;

public class AudioServiceTests
{
    [Fact]
    public void AudioConfig_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var config = new RealtimeDialog.Core.Configuration.AudioConfig();

        // Assert
        Assert.True(config.Input.SampleRate > 0);
        Assert.True(config.Input.Channels > 0);
        Assert.True(config.Output.SampleRate > 0);
        Assert.True(config.Output.Channels > 0);
    }

    [Fact]
    public void RealtimeDialogConfig_ShouldInitializeAudioConfig()
    {
        // Arrange & Act
        var config = new RealtimeDialogConfig
        {
            Audio = new RealtimeDialog.Core.Configuration.AudioConfig
            {
                Input = new RealtimeDialog.Core.Configuration.InputConfig
                {
                    SampleRate = 16000,
                    Channels = 1
                },
                Output = new RealtimeDialog.Core.Configuration.OutputConfig
                {
                    SampleRate = 24000,
                    Channels = 1
                }
            }
        };

        // Assert
        Assert.NotNull(config.Audio);
        Assert.Equal(16000, config.Audio.Input.SampleRate);
        Assert.Equal(24000, config.Audio.Output.SampleRate);
    }
    
    [Fact]
    public void AudioService_ShouldCreateSuccessfully()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<AudioService>>();
        var recorderMock = new Mock<AudioRecorder>(Mock.Of<ILogger<AudioRecorder>>());
        var playerMock = new Mock<AudioPlayer>(Mock.Of<ILogger<AudioPlayer>>());
        var webSocketClientMock = new Mock<WebSocketClient>(Mock.Of<ILogger<WebSocketClient>>());
        var clientServiceMock = new Mock<ClientRequestService>(Mock.Of<ILogger<ClientRequestService>>(), webSocketClientMock.Object);
        
        // Act & Assert - just verify construction doesn't throw
        var audioService = new AudioService(
            loggerMock.Object,
            recorderMock.Object,
            playerMock.Object,
            clientServiceMock.Object);
        
        Assert.NotNull(audioService);
    }
    
    [Fact]
    public void AudioService_ShouldManageUserQueryingState()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<AudioService>>();
        var recorderMock = new Mock<AudioRecorder>(Mock.Of<ILogger<AudioRecorder>>());
        var playerMock = new Mock<AudioPlayer>(Mock.Of<ILogger<AudioPlayer>>());
        var webSocketClientMock = new Mock<WebSocketClient>(Mock.Of<ILogger<WebSocketClient>>());
        var clientServiceMock = new Mock<ClientRequestService>(Mock.Of<ILogger<ClientRequestService>>(), webSocketClientMock.Object);
        
        var audioService = new AudioService(
            loggerMock.Object,
            recorderMock.Object,
            playerMock.Object,
            clientServiceMock.Object);
        
        // Act & Assert
        audioService.SetUserQueryingState(true);
        audioService.SetUserQueryingState(false);
        
        // Verify no exceptions thrown
        Assert.True(true);
    }
    
    [Fact]
    public void AudioService_ShouldManageAudioBuffer()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<AudioService>>();
        var recorderMock = new Mock<AudioRecorder>(Mock.Of<ILogger<AudioRecorder>>());
        var playerMock = new Mock<AudioPlayer>(Mock.Of<ILogger<AudioPlayer>>());
        var webSocketClientMock = new Mock<WebSocketClient>(Mock.Of<ILogger<WebSocketClient>>());
        var clientServiceMock = new Mock<ClientRequestService>(Mock.Of<ILogger<ClientRequestService>>(), webSocketClientMock.Object);
        
        var audioService = new AudioService(
            loggerMock.Object,
            recorderMock.Object,
            playerMock.Object,
            clientServiceMock.Object);
        
        var testAudioData = new byte[] { 0x00, 0x01, 0x02, 0x03 };
        
        // Act
        audioService.AddAudioToBuffer(testAudioData);
        audioService.ClearAudioBuffer();
        
        // Assert - verify no exceptions thrown
        Assert.True(true);
    }
}