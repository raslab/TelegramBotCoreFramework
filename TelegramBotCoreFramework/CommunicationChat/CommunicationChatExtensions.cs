using CommunicationChat.BotPrivateCommunication;
using CommunicationChat.MassSendings;
using Microsoft.Extensions.DependencyInjection;
using Helpers.PredefinedChannels;
using Telegram.Bot;

namespace CommunicationChat;

public static class CommunicationChatExtensions
{
    public static IServiceCollection AddCommunicationChannelsServices(this IServiceCollection services)
    {
        // subs BD
        services.AddSingleton<SupportProxyChannelHolderFactory>();
        
        services.AddSingleton<WelcomeBotCommunicationFactory>();
        services.AddSingleton<WelcomeBotSettings>();
        
        
        // mass messages
        services.AddSingleton<MassMessageSendingFactory>();
        services.AddHostedService(p => p.GetRequiredService<MassMessageSendingFactory>());
        services.AddSingleton<MassMessagesDeletingFactory>();
        services.AddHostedService(p => p.GetRequiredService<MassMessagesDeletingFactory>());
        
        // working with posts
        services.AddSingleton<SupportBotProxyFactory>();
        
        // bot clients
        services.AddSingleton<TelegramBotClientsFactory>();
        services.AddSingleton<TelegramBotClient>(s=>s.GetService<TelegramBotClientsFactory>()!.GetDefault());

        return services;
    }
}