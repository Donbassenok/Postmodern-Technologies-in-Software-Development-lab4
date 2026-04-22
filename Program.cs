using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureServices((context, services) =>
{
    services.AddSingleton<FrameProcessor>();
    
    services.AddHostedService<TelegramBotService>();
});

var host = builder.Build();
await host.RunAsync();
