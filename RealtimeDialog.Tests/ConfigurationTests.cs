using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using RealtimeDialog.Core.Configuration;

namespace RealtimeDialog.Tests;

public class ConfigurationTests
{
    [Fact]
    public void RealtimeDialogConfig_DefaultValues_ShouldBeValid()
    {
        // Arrange & Act
        var config = new RealtimeDialogConfig();

        // Assert
        Assert.NotNull(config.WebSocket);
        Assert.NotNull(config.Audio);
        Assert.NotNull(config.Dialog);
        Assert.NotNull(config.Logging);
    }

    [Fact]
    public void WebSocketConfig_DefaultValues_ShouldBeValid()
    {
        // Arrange & Act
        var config = new WebSocketConfig();

        // Assert
        Assert.NotNull(config.Url);
        Assert.True(config.ConnectionTimeoutMs > 0);
        Assert.True(config.ReceiveTimeoutMs > 0);
        Assert.NotNull(config.AppKey);
        Assert.NotNull(config.ResourceId);
    }

    [Fact]
    public void AudioConfig_DefaultValues_ShouldBeValid()
    {
        // Arrange & Act
        var config = new ConfigAudioConfig();

        // Assert
        Assert.NotNull(config.Input);
        Assert.NotNull(config.Output);
        Assert.True(config.Input.SampleRate > 0);
        Assert.True(config.Input.Channels > 0);
        Assert.True(config.Output.SampleRate > 0);
        Assert.True(config.Output.Channels > 0);
    }

    [Fact]
    public void DialogConfig_DefaultValues_ShouldBeValid()
    {
        // Arrange & Act
        var config = new DialogConfig();

        // Assert
        Assert.NotNull(config.BotName);
        Assert.NotNull(config.SystemRole);
        Assert.NotNull(config.SpeakingStyle);
        Assert.NotNull(config.Extra);
    }

    [Fact]
    public void LoggingConfig_DefaultValues_ShouldBeValid()
    {
        // Arrange & Act
        var config = new LoggingConfig();

        // Assert
        Assert.NotNull(config.LogLevel);
        Assert.NotNull(config.LogFilePath);
        Assert.True(config.EnableConsoleLogging || config.EnableFileLogging);
    }
}