using CommunicationChat;
using Helpers.UserAuth;
using Microsoft.Extensions.DependencyInjection;
using TG.UpdatesProcessing.AnalyticsRendering;
using TG.UpdatesProcessing.BotCommands;
using TG.UpdatesProcessing.BotCommands.BotSettings;
using TG.UpdatesProcessing.BotCommands.UserAuth;
using TG.UpdatesProcessing.PostsScheduling;
using TG.UpdatesProcessing.WelcomeBot;
using TG.UpdatesProcessing.WelcomeBotPostsScheduling;

namespace TG.UpdatesProcessing;

public static class TgUpdatesProcessingExtensions
{
    public static IServiceCollection AddTgUpdateProcessingServices(this IServiceCollection services)
    {
        // general processing
        services.AddHostedService<TgIngestionProcessingService>();
        
        
        // bot commands
        services.AddSingleton<IBotCommandsFactory, BotCommandsFactory>();
        services.AddSingleton<BotCommandsFactoryInitiator>();
        services.AddSingleton<BotUpdateProcessing>();
        services.AddSingleton<ChannelsInfoParser>();

        // general
        services.AddTransient<IBotCommand, MainMenuBotCommand>();
        
        // settings
        services.AddTransient<IBotCommand, UserAuthBotCommandController>();
        services.AddTransient<IBotCommand, BotSettingsSettingsBotCommand>();
        services.AddTransient<IBotCommand, ChannelsListBotCommandController>();
        
        // posts scheduling
        services.AddTransient<IBotCommand, PostsSchedulingBotCommand>();
        services.AddTransient<IBotCommand, ScheduleNewPostBotCommand>();
        services.AddTransient<IBotCommand, SchedulePostsListBotCommand>();
        services.AddSingleton<ScheduledMessagesSettings>();
        services.AddSingleton<ScheduledMessagesArchive>();
        services.AddHostedService<ScheduledMessagesDeliveryService>();
        services.AddSingleton<ScheduledMessagesPublisherHelper>();
        
        // analytics
        services.AddTransient<IBotCommand, AnalyticsRenderingRootBotCommand>();
        services.AddTransient<IBotCommand, CurrentStatsBotCommand>();
        
        // creatives generation
        services.AddTransient<IBotCommand, CreativesGenerationRootBotCommand>();
        services.AddTransient<IBotCommand, UpdateLinksInCreativeBotCommand>();
        services.AddTransient<IBotCommand, CreateSellAdsCreativeBotCommand>();
        services.AddTransient<IBotCommand, CreateCrossPrCreativeBotCommand>();
        services.AddTransient<IBotCommand, ConvertToHtmlCreativeBotCommand>();
        
        // channels join requests
        services.AddSingleton<RequestsBatchApprovalService>();
        services.AddHostedService(provider => provider.GetRequiredService<RequestsBatchApprovalService>());
        services.AddSingleton<ChannelJoinRequestsProcessor>();
        services.AddTransient<IBotCommand, WelcomeBotRootCommand>();
        services.AddTransient<IBotCommand, WelcomeBotSettingsCommand>();
        services.AddTransient<IBotCommand, ChatJoinRequestsApprovalBotCommand>();
        services.AddTransient<IBotCommand, WelcomeBotSubscribersBaseStatisticCommand>();
        
        // welcome bot posts scheduling
        services.AddHostedService<WelcomeBotScheduledMessagesDeliveryService>();
        services.AddTransient<IBotCommand, WelcomeBotSchedulePostCommand>();
        services.AddTransient<IBotCommand, WelcomeBotScheduleNewPostBotCommand>();
        services.AddTransient<IBotCommand, WelcomeBotSchedulePostsListBotCommand>();
        services.AddSingleton<WelcomeBotScheduledMessagesSettings>();
        services.AddSingleton<WelcomeBotScheduledMessagesArchive>();
        // services.AddHostedService<WelcomeBotScheduledMessagesDeliveryService>();
        services.AddSingleton<WelcomeBotScheduledMessagesPublisherHelper>();
        services.AddTransient<IBotCommand, WelcomeBotCaptchaReviewCommand>();
        services.AddTransient<IBotCommand, WelcomeBotWelcomeSequenceReviewCommand>();
        
            
        return services;
    }
}