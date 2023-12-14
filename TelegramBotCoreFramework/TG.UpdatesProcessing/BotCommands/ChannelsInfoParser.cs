using Helpers;
using Helpers.UserAuth;
using TG.UpdatesProcessing.BotCommands.UserAuth;
using TL;

namespace TG.UpdatesProcessing.BotCommands;

public class ChannelsInfoParser
{
    private readonly ChannelsSettings _calendar;
    private readonly TgUserAuthController _tgUserAuthController;
    private Channel[]? _channelsForAnalyse = null;

    public ChannelsInfoParser(
        ChannelsSettings calendar,
        TgUserAuthController tgUserAuthController)
    {
        _calendar = calendar;
        _tgUserAuthController = tgUserAuthController;
    }

    private async Task<long[]> GetChannelIdsToCollectAnalytics()
    {
        await _calendar.LoadSchedule();
        return _calendar.ChannelSettings.Select(e=>e.ChannelId).ToArray();
    }

    public async Task<Channel[]?> GetChannelsListForAnalysing()
    {
        if (_channelsForAnalyse != null)
            return _channelsForAnalyse;

        if (!_tgUserAuthController.IsLoggedIn())
            return Array.Empty<Channel>();
        await _tgUserAuthController.UserClient.LoginUserIfNeeded();
        
        var channelIds = await GetChannelIdsToCollectAnalytics();
        var chatIds = channelIds.Select(c=>c * -1 - 1000000000000).ToArray(); // -1001341648430 -> 1001341648430
        Dictionary<long, ChatBase> chats = null;
        while (chats == null)
        { 
            try
            {
                var dialogs = await _tgUserAuthController.UserClient.Messages_GetAllDialogs();
                chats = dialogs.chats;
            }
            catch (Exception e)
            {
                if (e.Message.Contains("FLOOD_WAIT_"))
                    await Task.Delay(1000 * _tgUserAuthController.UserClient.FloodRetryThreshold);
                else throw;
            }
        }
            
        return _channelsForAnalyse = chats.Values
            .OfType<Channel>()
            .Where(c=>c.admin_rights != null)
            .Where(c=>chatIds.Contains(c.id))
            .ToArray();
    }

    public async Task<(string originalLink, string migratedLink)[]> MigrateLinks(string[] links, string linkName, bool requiredAdminApproval)
    {
        if (!_tgUserAuthController.IsLoggedIn())
            return Array.Empty<(string originalLink, string migratedLink)>();
        await _tgUserAuthController.UserClient.LoginUserIfNeeded();
            
        var migratedLinks = new List<(string, string)>();
        foreach (var link in links)
        {
            InputPeer peer;
            if (link.StartsWith("https://t.me/c/"))
            {
                var channelId = link.Substring("https://t.me/c/".Length).Split("/")[0];
                var channels = await GetChannelsListForAnalysing();
                var channel = channels.FirstOrDefault(i => i.id.ToString() == channelId);
                peer = channel.ToInputPeer();
            }
            else
            {
                peer = (await _tgUserAuthController.UserClient.AnalyzeInviteLink(link)).ToInputPeer();
            }
            var migratedLink = await _tgUserAuthController.UserClient
                .Messages_ExportChatInvite(peer, title: linkName, request_needed: requiredAdminApproval);
            migratedLinks.Add(new (link, ((TL.ChatInviteExported)migratedLink).link));
        }
            
        return migratedLinks.ToArray();
    }
}