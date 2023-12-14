using Microsoft.Extensions.DependencyInjection;
using TG.UpdatesProcessing.BotCommands;

namespace SpecificToDevEnv;

public static class TgUpdatesProcessingExtensions
{
    public static IServiceCollection AddDevEnvSpecificBindings(this IServiceCollection services)
    {
        services.AddTransient<IBotCommand, DevSettingsBotCommand>();
        services.AddTransient<IBotCommand, PrintHelloWorldBotCommand>();
        return services;
    }
}