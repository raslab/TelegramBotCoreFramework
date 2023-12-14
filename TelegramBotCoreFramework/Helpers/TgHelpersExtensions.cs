using Google.Cloud.Firestore;
using Helpers.AdminsCommunication;
using Helpers.PredefinedChannels;
using Helpers.UserAuth;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;

namespace Helpers;

public static class TgHelpersExtensions
{
    public static IServiceCollection AddHelpersServices(this IServiceCollection services)
    {
        // general
        services.AddSingleton<ConfigurationStorage>();
        services.AddSingleton<ChannelsSettings>();
        services.AddSingleton<IUserInputAwaiting, UserInputAwaiting>();
        services.AddSingleton<AdminsController>();
        services.AddSingleton<AdminUsers>();
        
        // chats
        services.AddSingleton<ProjectTeamCommunication>();
        services.AddSingleton<LoggingChannel>();
        
        // auth
        services.AddTransient<FirestoreSessionStorage>();
        services.AddSingleton<TgUserAuthController>();
        
        // setup thirdparty
        services.AddSingleton(s => FirestoreDb.Create(Env.FirestoreProjectId));

        return services;
    }
}