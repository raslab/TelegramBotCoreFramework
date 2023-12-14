using Analytics.HistoricalData;
using Helpers;
using Helpers.AdminsCommunication;
using Helpers.Extensions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Update = Telegram.Bot.Types.Update;

namespace TG.UpdatesProcessing.BotCommands;

public class CreateCrossPrCreativeBotCommand : BotCommandControllerBase
{
    private readonly AdminsController _adminsController;
    private readonly ChannelsSettings _channelsSettings;
    private readonly AnalyticsDataHolder _analyticsDataHolder;
    public override string CommandName => "🤝 Взаємопіар";
    public override CommandsAccessLevel AccessLevel => CommandsAccessLevel.Manager;
    public override Type? ParentCommandType => typeof(CreativesGenerationRootBotCommand);
    
    public CreateCrossPrCreativeBotCommand(TelegramBotClient botClient, IBotCommandsFactory botCommandsFactory,
        AdminsController adminsController, ChannelsSettings channelsSettings,
        AnalyticsDataHolder analyticsDataHolder, AdminUsers adminUsers) 
        : base(botClient, botCommandsFactory, adminsController, adminUsers)
    {
        _adminsController = adminsController;
        _channelsSettings = channelsSettings;
        _analyticsDataHolder = analyticsDataHolder;
    }
    
    protected override async Task Build()
    {
        await _channelsSettings.LoadSchedule();
        
        AddDefaultShortcut(DefaultPathHandler);
    }

    private async Task<CommandResult> DefaultPathHandler(Update update, string[]? args, string? reRoutedForPath)
    {
        var me = await _adminsController.GetAdminUser(update.GetChatId());
        
        var channelLineTemplate = "{0}\n😌 Підписників: {1}\n👀 Переглядів (24): {2}+\n\n";
        var fullMessageTemplate = @"#ВП

{0}{1}
Для розміщення звертатись до @{2}";
        var networkInfoTemplate = @"Загально
😌 Підписників {0} 
👀 Переглядів за 24 години {1}+
👀 Переглядів за 48 годин {2}+
Можливе розміщення лише в декількох каналах. Підберемо пропозицію під Ваші охвати.
";

        await _channelsSettings.LoadSchedule();
        var bqGeneralChannelData = await _analyticsDataHolder.GetSubscribersCountFromBq();
        var messages24hData = await _analyticsDataHolder.GetChannelsPerformanceForPeriodAgo(24);
        var messages48hData = await _analyticsDataHolder.GetChannelsPerformanceForPeriodAgo(48);

        var infosTasks = _channelsSettings.ChannelSettings
            .Select(async c=>new 
            {
                c.ChannelId,
                scheduleInfo = c,
                info = await BotClient.GetChatAsync(new ChatId(c.ChannelId)), 
                count = await BotClient.GetChatMemberCountAsync(new ChatId(c.ChannelId))
            });
        var infos = await Task.WhenAll(infosTasks);

        var channelsDetailedPart = "";
        long totalSubs = 0;
        long total24hViews = 0;
        long total48hViews = 0;
        foreach (var info in infos)
        {
            var generalData = bqGeneralChannelData.FirstOrDefault(d=>d.ChannelId == info.ChannelId * -1 - 1000000000000);
            var messages24Data = messages24hData.FirstOrDefault(d=>d.ChannelId == info.ChannelId * -1 - 1000000000000);
            var messages48Data = messages48hData.FirstOrDefault(d=>d.ChannelId == info.ChannelId * -1 - 1000000000000);

            if (generalData == default || messages24Data == default || messages48Data == default)
            {
                continue;
            }

            totalSubs += generalData.SubscribersCount;
            total24hViews += messages24Data.Views;
            total48hViews += messages48Data.Views;

            var crossPrUrl = info.scheduleInfo.Params.ContainsKey(ChannelsSettings.CrossPrCreoUlrKey)
                ? $"<a href=\"{info.scheduleInfo.Params[ChannelsSettings.CrossPrCreoUlrKey]}\">{info.scheduleInfo.FullTitle}</a>"
                : info.scheduleInfo.GetHtmlUrl();
            channelsDetailedPart += string.Format(channelLineTemplate, crossPrUrl, generalData.SubscribersCount, messages24Data.Views);
        }

        var networkGeneralPart = "";
        if (infos.Length > 1)
        {
            networkGeneralPart = string.Format(networkInfoTemplate, totalSubs, total24hViews, total48hViews);
        }

        var message = string.Format(fullMessageTemplate, channelsDetailedPart, networkGeneralPart, me.Data.UserName);
        await ComposeMessage(update)
            .SetText(message)
            .SetNeedUpMenuButton()
            .SetNeedMainMenuButton()
            .Send();
        return CommandResult.Ok;
    }
}