using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RealtimeDialog.Core.Services;

namespace RealtimeDialog.Core.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRealtimeDialog(this IServiceCollection services, IConfiguration configuration)
    {
        // Register configuration
        services.Configure<RealtimeDialogConfig>(configuration.GetSection(RealtimeDialogConfig.SectionName));
        
        // Register core services
        services.AddSingleton<WebSocketClient>();
        services.AddSingleton<ClientRequestService>();
        services.AddSingleton<ServerResponseService>();
        
        // Register session and audio forwarding services
        services.AddSingleton<ISessionManager, InMemorySessionManager>();
        services.AddSingleton<IAudioForwardingService, DoubaoAudioForwardingService>();
        
        // Register main dialog service
        services.AddSingleton<RealtimeDialogService>();
        
        return services;
    }
    
    public static IServiceCollection AddRealtimeDialogLogging(this IServiceCollection services, IConfiguration configuration)
    {
        var loggingConfig = configuration.GetSection($"{RealtimeDialogConfig.SectionName}:Logging").Get<LoggingConfig>() ?? new LoggingConfig();
        
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            
            // Set log level
            if (Enum.TryParse<LogLevel>(loggingConfig.LogLevel, out var logLevel))
            {
                builder.SetMinimumLevel(logLevel);
            }
            
            // Add console logging
            if (loggingConfig.EnableConsoleLogging)
            {
                builder.AddConsole(options =>
                {
                    options.IncludeScopes = true;
                    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
                });
            }
            
            // Add file logging if enabled (would need additional package like Serilog)
            if (loggingConfig.EnableFileLogging)
            {
                // Note: For file logging, you might want to add Serilog or NLog
                // This is a placeholder for file logging configuration
                builder.AddDebug();
            }
        });
        
        return services;
    }
    
    public static IServiceCollection AddRealtimeDialogConfiguration(this IServiceCollection services)
    {
        services.AddSingleton<IConfiguration>(provider =>
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true)
                .AddEnvironmentVariables();
                
            return builder.Build();
        });
        
        return services;
    }
}