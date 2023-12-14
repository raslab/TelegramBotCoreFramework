using Analytics.UsersDatabase;
using Google.Cloud.BigQuery.V2;
using Helpers;
using Helpers.UserAuth;
using Microsoft.Extensions.Logging;
using TL;

namespace Analytics.HistoricalData;

public class ChannelsAnalyticsCollecting
{
    private readonly ILogger<ChannelsAnalyticsCollecting> _logger;
    private readonly TgUserAuthController _tgUserAuthController;
    private readonly BigQueryClient _bqClient;
    private readonly ChannelsSettings _channelsSettings;
    // private readonly SubscribersDatabase _subscribersDatabase;
    Channel[] _channelsForAnalyse = null;

    public ChannelsAnalyticsCollecting(
        ILogger<ChannelsAnalyticsCollecting> logger,
        TgUserAuthController tgUserAuthController, 
        BigQueryClient bqClient, 
        ChannelsSettings channelsSettings)
    {
        _logger = logger;
        _tgUserAuthController = tgUserAuthController;
        _bqClient = bqClient;
        _channelsSettings = channelsSettings;
        // _subscribersDatabase = subscribersDatabase;
    }

    private async Task<long[]> GetChannelIdsToCollectAnalytics()
    {
        await _channelsSettings.LoadSchedule();
        return _channelsSettings.ChannelSettings.Select(e=>e.ChannelId).ToArray();
    }

    public async Task<Channel[]> GetChannelsListForAnalysing()
    {
        if (!_tgUserAuthController.IsLoggedIn())
        {
            _logger.LogWarning($"TG user not authentificated, can't collect analytics");
            return Array.Empty<Channel>();
        }
        await _tgUserAuthController.UserClient.LoginUserIfNeeded();
        
        if (_channelsForAnalyse != null)
            return _channelsForAnalyse;
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

    public async Task<int> CollectAndStoreLastMessagesViewsAnalytics()
    {
        if (!_tgUserAuthController.IsLoggedIn())
        {
            _logger.LogWarning($"TG user not authentificated, can't collect analytics");
            return 0;
        }
        await _tgUserAuthController.UserClient.LoginUserIfNeeded();
        
        var channels = await GetChannelsListForAnalysing();

        var forStore = new List<ChannelMessage>();
        foreach (var channel in channels)
        {
            var inputC = new InputChannel(channel.id, channel.access_hash);
            var fullChannel = await _tgUserAuthController.UserClient.Channels_GetFullChannel(inputC);
            var messages = await _tgUserAuthController.UserClient.Messages_GetHistory(channel.ToInputPeer(), limit: 20);
            forStore.AddRange(messages.Messages.OfType<Message>()
                .Select(m =>
                    new ChannelMessage()
                    {
                        ChannelId = channel.id,
                        Date = m.Date,
                        Forwards = m.forwards,
                        Reactions = m.reactions?.results.Sum(r=>r.count) ?? 0,
                        Views = m.views,
                        MessageId = m.id,
                        ReactionsFull = String.Join(",",m.reactions?.results.Select(r=>$"{(r.reaction as ReactionEmoji).emoticon}:{r.count}") ?? Array.Empty<string>()),
                        Er = (1f * (m.forwards + m.reactions?.results.Sum(r=>r.count) ?? 0)) / m.views,
                        Err = (1f * m.views) / fullChannel.full_chat.ParticipantsCount
                    })
            );
        }
            
        var table = _bqClient.GetTable(Env.BigQueryDatasetId, Env.MessagesTableName);
        var insert = await table.InsertRowsAsync(forStore.Select(m=>m.ToRow()), new InsertOptions { AllowUnknownFields = true, AllowEmptyInsertIds = true});
        return forStore.Count;
    }

    /// to run once per day
    public async Task<int> CollectAndStoreChannelsParticipantsAnalytics()
    {
        if (!_tgUserAuthController.IsLoggedIn())
        {
            _logger.LogWarning($"TG user not authentificated, can't collect analytics");
            return 0;
        }
        await _tgUserAuthController.UserClient.LoginUserIfNeeded();
        
        
        var channels = await GetChannelsListForAnalysing();

        var addedCount = 0;
        foreach (var channel in channels)
        {   
            var inputC = new InputChannel(channel.id, channel.access_hash);
            var channelUsersRaw = await _tgUserAuthController.UserClient.Channels_GetAllParticipants(inputC);
            // await _subscribersDatabase.CheckParticipantsAndAddMissed(channel, channelUsersRaw);
            var channelUsers = new List<ChannelUser>(channelUsersRaw.users.Select(p=>new ChannelUser()
            {
                ChannelId = channel.id,
                UserId = p.Value.id
            }));
            var table = _bqClient.GetTable(Env.BigQueryDatasetId, Env.UsersTableName);
            var insert = await table.InsertRowsAsync(channelUsers.Select(m=>m.ToRow()), new InsertOptions { AllowUnknownFields = true, AllowEmptyInsertIds = true});
            addedCount += channelUsers.Count;
        }

        // await _subscribersDatabase.FlushParticipantsCheckAndCleanup();
        
        return addedCount;
    }

    public async Task<int> CollectAndStoreChannelsSubscribersCountAnalytics()
    {
        if (!_tgUserAuthController.IsLoggedIn())
        {
            _logger.LogWarning($"TG user not authentificated, can't collect analytics");
            return 0;
        }
        await _tgUserAuthController.UserClient.LoginUserIfNeeded();
        
        var channels = await GetChannelsListForAnalysing();

        var channelsGeneralInfo = new List<ChannelGeneralInfo>();
        foreach (var channel in channels)
        {
            var inputC = new InputChannel(channel.id, channel.access_hash);
            var fullChannel = await _tgUserAuthController.UserClient.Channels_GetFullChannel(inputC);
            channelsGeneralInfo.Add(new ChannelGeneralInfo(){
                ChannelId = channel.id,
                SubscribersCount = fullChannel.full_chat.ParticipantsCount
            });
        }
            
        var table = _bqClient.GetTable(Env.BigQueryDatasetId, Env.GeneralInformationTableName);
        var insert = await table.InsertRowsAsync(channelsGeneralInfo.Select(m=>m.ToRow()), new InsertOptions { AllowUnknownFields = true, AllowEmptyInsertIds = true});
        return channelsGeneralInfo.Count;
    }

    public async Task<int> CollectAndStoreChannelsAdminLog()
    {
        if (!_tgUserAuthController.IsLoggedIn())
        {
            _logger.LogWarning($"TG user not authentificated, can't collect analytics");
            return 0;
        }
        await _tgUserAuthController.UserClient.LoginUserIfNeeded();
        
        string getLastLogDateTimeQueryTemplate = $"SELECT max(Date) as Date FROM `{Env.BigQueryProjectId}.{Env.BigQueryDatasetId}.channels_admin_log` WHERE ChannelId = ?";

        var channels = await GetChannelsListForAnalysing();
        var table = _bqClient.GetTable(Env.BigQueryDatasetId, Env.AdminLogTableName);
            
        var addedCount = 0;
        foreach (var channel in channels.Where(c=>!c.title.Contains("Test")))
        {
            BigQueryParameter[] parameters = new[]
            {
                new BigQueryParameter(BigQueryDbType.Int64, channel.id)
            };
            QueryOptions queryOptions = new QueryOptions { ParameterMode = BigQueryParameterMode.Positional };
            BigQueryResults results = _bqClient.ExecuteQuery(getLastLogDateTimeQueryTemplate, parameters, queryOptions);
            var lastLogDate = results.Count() == 0
                ? new DateTime()
                : (DateTime)results.First()["Date"];

            var inputC = new InputChannel(channel.id, channel.access_hash);
            var adminLog = await _tgUserAuthController.UserClient.Channels_GetAdminLog(inputC);

            //     Channel admin log event
            //     See https://corefork.telegram.org/type/ChannelAdminLogEventAction
            //     Derived classes: TL.ChannelAdminLogEventActionChangeTitle, TL.ChannelAdminLogEventActionChangeAbout,
            //     TL.ChannelAdminLogEventActionChangeUsername, TL.ChannelAdminLogEventActionChangePhoto,
            //     TL.ChannelAdminLogEventActionToggleInvites, TL.ChannelAdminLogEventActionToggleSignatures,
            //     TL.ChannelAdminLogEventActionUpdatePinned, TL.ChannelAdminLogEventActionEditMessage,
            //     TL.ChannelAdminLogEventActionDeleteMessage, TL.ChannelAdminLogEventActionParticipantJoin,
            //     TL.ChannelAdminLogEventActionParticipantLeave, TL.ChannelAdminLogEventActionParticipantInvite,
            //     TL.ChannelAdminLogEventActionParticipantToggleBan, TL.ChannelAdminLogEventActionParticipantToggleAdmin,
            //     TL.ChannelAdminLogEventActionChangeStickerSet, TL.ChannelAdminLogEventActionTogglePreHistoryHidden,
            //     TL.ChannelAdminLogEventActionDefaultBannedRights, TL.ChannelAdminLogEventActionStopPoll,
            //     TL.ChannelAdminLogEventActionChangeLinkedChat, TL.ChannelAdminLogEventActionChangeLocation,
            //     TL.ChannelAdminLogEventActionToggleSlowMode, TL.ChannelAdminLogEventActionStartGroupCall,
            //     TL.ChannelAdminLogEventActionDiscardGroupCall, TL.ChannelAdminLogEventActionParticipantMute,
            //     TL.ChannelAdminLogEventActionParticipantUnmute, TL.ChannelAdminLogEventActionToggleGroupCallSetting,
            //     TL.ChannelAdminLogEventActionParticipantJoinByInvite, TL.ChannelAdminLogEventActionExportedInviteDelete,
            //     TL.ChannelAdminLogEventActionExportedInviteRevoke, TL.ChannelAdminLogEventActionExportedInviteEdit,
            //     TL.ChannelAdminLogEventActionParticipantVolume, TL.ChannelAdminLogEventActionChangeHistoryTTL,
            //     TL.ChannelAdminLogEventActionParticipantJoinByRequest, TL.ChannelAdminLogEventActionToggleNoForwards,
            //     TL.ChannelAdminLogEventActionSendMessage, TL.ChannelAdminLogEventActionChangeAvailableReactions,
            //     TL.ChannelAdminLogEventActionChangeUsernames, TL.ChannelAdminLogEventActionToggleForum,
            //     TL.ChannelAdminLogEventActionCreateTopic, TL.ChannelAdminLogEventActionEditTopic,
            //     TL.ChannelAdminLogEventActionDeleteTopic, TL.ChannelAdminLogEventActionPinTopic,
            //     TL.ChannelAdminLogEventActionToggleAntiSpam

            var newEvents = adminLog.events.Where(e=>e.date > lastLogDate).ToArray();
            if (newEvents.Length == 0)
                continue;
            var channelLog = new List<ChannelAdminLogEventEntity>(newEvents.Select(p =>
            {
                var entity =  new ChannelAdminLogEventEntity()
                {
                    EventId = p.id,
                    ChannelId = channel.id,
                    UserId = p.user_id,
                    Date = p.date
                };
                if (p.action is ChannelAdminLogEventActionParticipantLeave)
                    entity.Action = "leave";
                else if (p.action is ChannelAdminLogEventActionParticipantJoin)
                {
                    entity.Action = "join";
                    entity.InviteLink = "organic";
                    entity.InviteLinkName = "organic";
                }
                else if (p.action is ChannelAdminLogEventActionParticipantJoinByInvite)
                {
                    var invite = (p.action as ChannelAdminLogEventActionParticipantJoinByInvite).invite as ChatInviteExported;
                    entity.Action = "invite";
                    entity.InviteLink = invite.link;
                    entity.InviteLinkName = invite.title;
                    entity.AdminId = invite.admin_id;
                }
                else if (p.action is ChannelAdminLogEventActionParticipantJoinByRequest)
                {
                    var invite = (p.action as ChannelAdminLogEventActionParticipantJoinByRequest).invite as ChatInviteExported;
                    entity.Action = "approve";
                    entity.InviteLink = invite.link;
                    entity.InviteLinkName = invite.title;
                    entity.AdminId = invite.admin_id;
                }
                else
                {
                    entity.Action = p.action.GetType().Name;
                }
                return entity;
            }));
                
            var insert = await table.InsertRowsAsync(channelLog.Select(m=>m.ToRow()), new InsertOptions { AllowUnknownFields = true, AllowEmptyInsertIds = true});
            addedCount += channelLog.Count;
        }
        return addedCount;
    }
}