using Microsoft.Extensions.DependencyInjection;

namespace TG.Webhooks.Processing;

public static class TgWebhooksProcessingExtensions
{
    public static IServiceCollection AddWebhookProcessingServices(this IServiceCollection services)
    {   
        services.AddTransient<SetupBotWebhooksHelper>();
        services.AddSingleton<WebhookUpdateMessagesIngestion>();

        return services;
    }
}