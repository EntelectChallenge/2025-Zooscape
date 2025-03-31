using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using S3Logger;
using S3Logger.Events;
using S3Logger.Utilities;
using Serilog;
using Serilog.Context;
using Serilog.Extensions.Logging;
using Zooscape.Application;
using Zooscape.Application.Config;
using Zooscape.Application.Events;
using Zooscape.Application.Services;
using Zooscape.Infrastructure.CloudIntegration.Enums;
using Zooscape.Infrastructure.CloudIntegration.Events;
using Zooscape.Infrastructure.CloudIntegration.Models;
using Zooscape.Infrastructure.CloudIntegration.Services;
using Zooscape.Infrastructure.SignalRHub.Config;
using Zooscape.Infrastructure.SignalRHub.Events;
using Zooscape.Infrastructure.SignalRHub.Hubs;

using (LogContext.PushProperty("ConsoleOnly", true))
{
    Log.Information("Initialising cloud integration");

    // Initialize CloudIntegrationService manually so we can announce failures early
    var cloudLog = new SerilogLoggerFactory(Log.Logger).CreateLogger<CloudIntegrationService>();
    CloudSettings cloudSettings = new();
    CloudIntegrationService cloudIntegrationService = new(cloudSettings, cloudLog);

    Log.Information("Announcing initialisation to cloud");
    await cloudIntegrationService.Announce(CloudCallbackType.Initializing);
    try
    {
        Log.Information("Initialising host");
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices(
                (context, services) =>
                {
                    // Cloud Integration
                    services.AddSingleton<ICloudIntegrationService>(cloudIntegrationService);

                    // SignalR
                    services.Configure<SignalRConfigOptions>(
                        context.Configuration.GetSection("SignalR")
                    );
                    services.AddSignalR(options =>
                    {
                        options.EnableDetailedErrors = true;
                        options.MaximumReceiveMessageSize = 40000000;
                        options.KeepAliveInterval = TimeSpan.FromSeconds(15);
                        options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
                    });

                    services.Configure<GameSettings>(
                        context.Configuration.GetSection("GameSettings")
                    );

                    services.Configure<S3Configuration>(
                        context.Configuration.GetSection("S3Configuration")
                    );

                    services.AddKeyedSingleton<IEventDispatcher, SignalREventDispatcher>("signalr");
                    services.AddKeyedSingleton<IEventDispatcher, CloudEventDispatcher>("cloud");
                    services.AddKeyedSingleton<IEventDispatcher, LogStateEventDispatcher>(
                        "logState"
                    );

                    services.AddSingleton<IStreamingFileLogger>(
                        new StreamingFileLogger("gameLogs.log")
                    );

                    services.AddSingleton<IZookeeperService, ZookeeperService>();

                    services.AddTransient<BotHub>();

                    services.AddSingleton<IGameStateService, GameStateService>();

                    services.AddHostedService<WorkerService>();
                }
            )
            .ConfigureWebHostDefaults(webBuilder =>
            {
                var port = 5001;
                Log.Information("Configuring SignalR to run on port {port}", port);
                webBuilder.UseUrls($"http://*:{port}");
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapHub<BotHub>("/bothub");
                    });
                });
            })
            .UseSerilog(
                (context, services, serilogConfig) =>
                    serilogConfig.ReadFrom.Configuration(context.Configuration)
            )
            .Build();
        Log.Information("Running host");
        host.Run();
    }
    catch (Exception ex)
    {
        Log.Fatal($"Error starting host: {ex}");
        await cloudIntegrationService.Announce(CloudCallbackType.Failed, ex);
        await S3.UploadLogs();
        await cloudIntegrationService.Announce(CloudCallbackType.LoggingComplete);
        throw;
    }
}
