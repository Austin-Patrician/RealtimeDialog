using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RealtimeDialog.Core.Configuration;
using RealtimeDialog.Core.Hubs;
using RealtimeDialog.Core.Services;

namespace RealtimeDialog.Core;

class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container
        builder.Services.AddRealtimeDialogConfiguration();
        builder.Services.AddRealtimeDialogLogging(builder.Configuration);
        builder.Services.AddRealtimeDialog(builder.Configuration);

        // Add SignalR
        builder.Services.AddSignalR();

        // Add CORS
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowFrontend", policy =>
            {
                policy.WithOrigins("http://localhost:5173", "http://localhost:3000") // Vite and React dev servers
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
        });

        var app = builder.Build();

        // Configure the HTTP request pipeline
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseRouting();
        app.UseCors("AllowFrontend");

        // Map SignalR hub
        app.MapHub<ConversationHub>("/conversationHub");

        // Add a simple health check endpoint
        app.MapGet("/health", () => "OK");

        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("🚀 RealtimeDialog backend server starting...");
        
        app.Run();
    }
}