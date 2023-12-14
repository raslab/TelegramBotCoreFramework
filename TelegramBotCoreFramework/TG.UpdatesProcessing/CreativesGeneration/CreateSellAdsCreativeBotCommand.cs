using Analytics.HistoricalData;
using Helpers;
using Helpers.AdminsCommunication;
using Helpers.Extensions;
using Microsoft.VisualBasic;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TG.UpdatesProcessing.BotCommands;

public class CreateSellAdsCreativeBotCommand : BotCommandControllerBase
{
    private readonly AdminsController _adminsController;
    private readonly AnalyticsDataHolder _analyticsDataHolder;
    private readonly ChannelsSettings _channelsSettings;
    public override string CommandName => "💰 Продаж реклами на біржах";
    public override CommandsAccessLevel AccessLevel => CommandsAccessLevel.Manager;
    public override Type? ParentCommandType => typeof(CreativesGenerationRootBotCommand);
    
    public CreateSellAdsCreativeBotCommand(TelegramBotClient botClient, IBotCommandsFactory botCommandsFactory,
        AdminsController adminsController, AnalyticsDataHolder analyticsDataHolder,
        ChannelsSettings channelsSettings, AdminUsers adminUsers) 
        : base(botClient, botCommandsFactory, adminsController, adminUsers)
    {
        _adminsController = adminsController;
        _analyticsDataHolder = analyticsDataHolder;
        _channelsSettings = channelsSettings;
    }
    
    protected override async Task Build()
    {
        await _channelsSettings.LoadSchedule();

        AddDefaultShortcut(DefaultPathHandler);
    }

    private async Task<CommandResult> DefaultPathHandler(Update update, string[]? args, string? reRoutedForPath)
    {
        var me = await _adminsController.GetAdminUser(update.GetChatId());
        
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

        var channelsStats = infos.Select(i => new
            {
                info = i,
                generalData = bqGeneralChannelData.FirstOrDefault(d=>d.ChannelId == (i.ChannelId * -1) - 1000000000000),
                messages24Data = messages24hData.FirstOrDefault(d=>d.ChannelId == (i.ChannelId * -1) - 1000000000000),
                messages48Data = messages48hData.FirstOrDefault(d=>d.ChannelId == (i.ChannelId * -1) - 1000000000000),
                nameParts = i.info.Title.Split(" ")
            })
            .Where(i=>i.generalData != default && i.messages24Data != default && i.messages48Data != default)
            .OrderByDescending(i => i.messages24Data.Views)
            .ToArray();

        
        var channelLineTemplate = "{0}{1}\n👥 Підписників: {2}\n💳 <b>{3} CPM</b>\n1/24 - 👁 {4}+, 💰 {5} грн\n1/48 - 👁 {6}+, 💰 {7} грн\n";
        var networkPartTemplate = @"🎯 <b>ВСІ {0} КАНАЛИ</B>
👥 Підписників: {1}
🔥 <b>{2} CPM</b>
1/24 - 👁 {3}+, 💰 {4} грн
1/48 - 👁 {5}+, 💰 {6} грн";
        var fullMessageTemplate = @"#ПродамРекламу #ВП
{0}
{1}
📨 Для розміщення реклами
Звертайтеся до @{2}";

        
        var totalCost24 = 0;
        var totalCost48 = 0;
        
        var channelsDetailedInfo = "";
        foreach (var channelsStat in channelsStats)
        {
            var fullCpm = channelsStat.info.scheduleInfo.Params.ContainsKey(ChannelsSettings.CpmFullCostKey) &&
                          int.TryParse(channelsStat.info.scheduleInfo.Params[ChannelsSettings.CpmFullCostKey],
                              out int fullCpmParsed)
                ? fullCpmParsed
                : 130;
            var discountCpm = channelsStat.info.scheduleInfo.Params.ContainsKey(ChannelsSettings.CpmDiscountCostKey) &&
                          int.TryParse(channelsStat.info.scheduleInfo.Params[ChannelsSettings.CpmDiscountCostKey],
                              out int discountCpmParsed)
                ? discountCpmParsed
                : 110;
            var cost24 = GetCost(channelsStat.messages24Data.Views, fullCpm);
            var cost48 = GetCost(channelsStat.messages24Data.Views, fullCpm);
            totalCost24 += GetCost(channelsStat.messages48Data.Views, discountCpm);
            totalCost48 += GetCost(channelsStat.messages48Data.Views, discountCpm);
            
            
            var sellUrl = channelsStat.info.scheduleInfo.GetHtmlUrl(ifExistsGetLinkFromParam:ChannelsSettings.AdSellCreoUrlKey);
            
            channelsDetailedInfo += string.Format(channelLineTemplate, sellUrl, "", 
                channelsStat.generalData.SubscribersCount, fullCpm, channelsStat.messages24Data.Views, cost24, channelsStat.messages48Data.Views, cost48);
        }

        var networkInfo = "";
        if (channelsStats.Length > 1)
        {
            long totalSubs = channelsStats.Sum(i => i.generalData.SubscribersCount);
            long total24hViews = channelsStats.Sum(i => i.messages24Data.Views);
            long total48hViews = channelsStats.Sum(i => i.messages48Data.Views);
            var cpm = (int)(totalCost24 / (total24hViews / 1000f));

            networkInfo = string.Format(networkPartTemplate, channelsStats.Length, totalSubs, cpm, total24hViews,
                totalCost24, total48hViews, totalCost48);
        }

        var message = string.Format(fullMessageTemplate, networkInfo, channelsDetailedInfo, me.Data.UserName);

        await ComposeMessage(update)
            .SetText(message)
            .SetNeedUpMenuButton()
            .SetNeedMainMenuButton()
            .Send();
        return CommandResult.Ok;
    }
    
    private static int GetCost(long views, int cpm)
    {
        return ((int)((views / 1000f) * cpm) / 10) * 10;
    }
}