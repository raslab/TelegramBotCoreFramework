using Analytics.HistoricalData;
using Helpers;
using Helpers.AdminsCommunication;
using Telegram.Bot;
using Telegram.Bot.Types;
using TG.UpdatesProcessing.BotCommands;

namespace TG.UpdatesProcessing.AnalyticsRendering;

public class CurrentStatsBotCommand : BotCommandBase
{
    private readonly AnalyticsDataHolder _analyticsDataHolder;
    private readonly ChannelsSettings _channelsSettings;

    public CurrentStatsBotCommand(TelegramBotClient botClient, IBotCommandsFactory botCommandsFactory,
        AnalyticsDataHolder analyticsDataHolder,
        ChannelsSettings channelsSettings, AdminsController adminsController, AdminUsers adminUsers) 
        : base(botClient, botCommandsFactory, adminsController, adminUsers)
    {
        _analyticsDataHolder = analyticsDataHolder;
        _channelsSettings = channelsSettings;
    }

    public override string CommandName => "🕙 Поточні дані";
    public override CommandsAccessLevel AccessLevel => CommandsAccessLevel.Owner;
    public override Type? ParentCommandType => typeof(AnalyticsRenderingRootBotCommand);

    public override async Task<CommandResult> ProcessMessage(Update update, string[]? args,
        string? reroutedForPath)
    {
        await _channelsSettings.LoadSchedule();
        var bqGeneralChannelData = await _analyticsDataHolder.GetSubscribersCountFromBq();
        var messages24hData = await _analyticsDataHolder.GetChannelsPerformanceForPeriodAgo(24);
        var messages48hData = await _analyticsDataHolder.GetChannelsPerformanceForPeriodAgo(48);

        var infosTasks = _channelsSettings.ChannelSettings
            .Select(async c=>new 
            {
                Channel = c,
                ChannelId = c.ChannelId, 
                info = await BotClient.GetChatAsync(new ChatId(c.ChannelId)), 
                count = await BotClient.GetChatMemberCountAsync(new ChatId(c.ChannelId))
            });
        var infos = await Task.WhenAll(infosTasks);

        var infosMessage = "<b>Дані по кожному каналу</b>\n\n";
        long totalSubs = 0;
        long total24hViews = 0;
        long total48hViews = 0;
        long totalReactions = 0;
        long totalForwards = 0;
        foreach (var info in infos)
        {
            var generalData = bqGeneralChannelData.FirstOrDefault(d=>d.ChannelId == info.ChannelId * -1 - 1000000000000);
            var messages24Data = messages24hData.FirstOrDefault(d=>d.ChannelId == info.ChannelId * -1 - 1000000000000);
            var messages48Data = messages48hData.FirstOrDefault(d=>d.ChannelId == info.ChannelId * -1 - 1000000000000);

            if (generalData == default || messages24Data == default || messages48Data == default)
            {
                infosMessage += $"- Мало даних по каналу {info.Channel.GetHtmlUrl()}, треба дані за 48+ годин.\n";
                continue;
            }

            totalSubs += generalData.SubscribersCount;
            total24hViews += messages24Data.Views;
            total48hViews += messages48Data.Views;
            totalReactions += messages24Data.Reactions;
            totalForwards += messages24Data.Forwards;
                 
            infosMessage += $"{info.Channel.GetHtmlUrl()}\n";
            infosMessage += $"😌 {generalData.SubscribersCount} | 👀+Err24 {messages24Data.Views}/{messages24Data.Views * 1f / generalData.SubscribersCount * 100:#.##}% | 👀+Err48 {messages48Data.Views}/{messages48Data.Views * 1f / generalData.SubscribersCount * 100:#.##}%\n\n";
        }

        infosMessage += $"\n<b>Загальна інформація</b>\n";
        infosMessage += $"\nВсього підписників: {totalSubs}";
        infosMessage += $"\nВсього переглядів 24г.: {total24hViews} \n" +
                        $"Всього переглядів 48г.: {total48hViews}";
        infosMessage += $"\nВсього реакцій: {totalReactions} \n" +
                        $"Всього пересилань: {totalForwards}";
        infosMessage += $"\nEr24 = {(totalForwards + totalReactions) * 1f / total24hViews * 100:#.##}%";
        infosMessage += $"\nErr24 = {total24hViews * 1f / totalSubs * 100:#.##}% | Err48 = {total48hViews * 1f / totalSubs * 100:#.##}%\n";

        if (infos.Length > 1)
        {
            var audienceInfo = await _analyticsDataHolder.GetAudienceInfo();
            var totalAudienceCalculated = audienceInfo.Sum(a => a.UsersCount * a.ChannelsCount);
            var calculationError = totalSubs * 1f / totalAudienceCalculated;
            var uniqueUsers = audienceInfo.Sum(a => a.UsersCount);
            infosMessage += $"\n<b>Інформація по сітці</b>\n";
            infosMessage += $"\nВсього знайдено підписників: {totalAudienceCalculated * calculationError:#}";
            infosMessage += $"\nУнікальних підписників {uniqueUsers} ({uniqueUsers * 1f / totalSubs * 100:0.#}% of total subs)";
            infosMessage += $"\nМожлива похибка в розрахунку до {Math.Abs(calculationError * 100 - 100):0.#}%";
            infosMessage += $"\n\nN | Підписантів на N каналів";
            foreach (var audience in audienceInfo)
            {
                infosMessage += $"\n{audience.ChannelsCount} | {audience.UsersCount * calculationError:#}";
            }
        }

        await ComposeMessage(update)
            .SetText(infosMessage)
            .SetNeedUpMenuButton()
            .SetNeedMainMenuButton()
            .Send();

        return CommandResult.Ok;
    }
}