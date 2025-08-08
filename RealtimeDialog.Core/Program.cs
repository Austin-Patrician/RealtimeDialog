using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RealtimeDialog.Core.Configuration;
using RealtimeDialog.Core.Services;

namespace RealtimeDialog.Core;

class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            Console.WriteLine("Starting application...");

            // Build host
            var host = CreateHostBuilder(args).Build();
            
            // Get services
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var realtimeDialogService = host.Services.GetRequiredService<RealtimeDialogService>();
            
            // Setup cancellation token for graceful shutdown
            using var cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cancellationTokenSource.Cancel();
                logger.LogInformation("Shutdown requested by user");
            };

            // Subscribe to service events
            realtimeDialogService.ConnectionEstablished += () => 
                logger.LogInformation("✓ WebSocket connection established");
            
            realtimeDialogService.ConnectionLost += () => 
                logger.LogInformation("✗ WebSocket connection lost");
            
            realtimeDialogService.SessionStarted += dialogId => 
                logger.LogInformation("✓ Dialog session started with ID: {DialogId}", dialogId);
            
            realtimeDialogService.SessionEnded += () => 
                logger.LogInformation("✗ Dialog session ended");
            
            realtimeDialogService.UserQueryDetected += () => 
                logger.LogInformation("🎤 User started speaking");
            
            realtimeDialogService.UserQueryFinished += () => 
                logger.LogInformation("🔇 User finished speaking");

            // Validate configuration
            var config = host.Services.GetRequiredService<IConfiguration>();
            var appId = config["RealtimeDialog:WebSocket:AppId"];
            var accessToken = config["RealtimeDialog:WebSocket:AccessToken"];
            
            if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(accessToken))
            {
                logger.LogError("AppId and AccessToken must be configured in appsettings.json");
                Console.WriteLine("\n❌ Configuration Error:");
                return 1;
            }

            // Start the realtime dialog service
            logger.LogInformation("Starting RealtimeDialog service...");
            Console.WriteLine("\n🚀 Connecting to ByteDance Realtime Dialog API...");
            
            var startResult = await realtimeDialogService.StartAsync(cancellationTokenSource.Token);
            if (!startResult)
            {
                logger.LogError("Failed to start RealtimeDialog service");
                return 1;
            }
            
            Console.WriteLine("" + new string('=', 50));

            // Keep the application running until cancellation is requested
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Application shutdown requested");
            }

            // Graceful shutdown
            Console.WriteLine("\n🛑 Shutting down...");
            await realtimeDialogService.StopAsync();
            
            logger.LogInformation("Application stopped gracefully");
            Console.WriteLine("✅ Application stopped successfully");
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Fatal error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return 1;
        }
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(Directory.GetCurrentDirectory())
                      .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                      .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true)
                      .AddEnvironmentVariables()
                      .AddCommandLine(args);
            })
            .ConfigureServices((context, services) =>
            {
                // Add RealtimeDialog services
                services.AddRealtimeDialogConfiguration();
                services.AddRealtimeDialogLogging(context.Configuration);
                services.AddRealtimeDialog(context.Configuration);
            })
            .UseConsoleLifetime();
}