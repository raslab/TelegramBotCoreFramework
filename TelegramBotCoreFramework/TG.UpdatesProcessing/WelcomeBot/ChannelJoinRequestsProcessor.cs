using Analytics.UsersDatabase;
using CommunicationChat.BotPrivateCommunication;
using CommunicationChat.MassSendings;
using Helpers;
using Helpers.Extensions;
using Helpers.PredefinedChannels;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using MessageType = Analytics.UsersDatabase.MessageType;

namespace TG.UpdatesProcessing.WelcomeBot;

public class ChannelJoinRequestsProcessor 
{
    private readonly WelcomeBotSettings _welcomeBotSettings;
    private readonly SubscribersDatabase _subscribersDatabase;
    private readonly TelegramBotClient _botClient;
    private readonly LoggingChannel _loggingChannel;
    private readonly ChannelsSettings _channelsSettings;
    private readonly MassMessageSendingFactory _massMessageSendingFactory;

    public ChannelJoinRequestsProcessor(WelcomeBotSettings welcomeBotSettings, 
        SubscribersDatabase subscribersDatabase,
        TelegramBotClient botClient, 
        LoggingChannel loggingChannel,
        ChannelsSettings channelsSettings,
        MassMessageSendingFactory massMessageSendingFactory)
    {
        _welcomeBotSettings = welcomeBotSettings;
        _subscribersDatabase = subscribersDatabase;
        _botClient = botClient;
        _loggingChannel = loggingChannel;
        _channelsSettings = channelsSettings;
        _massMessageSendingFactory = massMessageSendingFactory;
    }

    internal async Task JoinRequestHandle(ChatJoinRequest? chatJoinRequest)
    {
        await _welcomeBotSettings.LoadDefaultIfNeeded();
        
        var sub = await _subscribersDatabase.GetSubscriber(chatJoinRequest.From.Id);

        if (chatJoinRequest.InviteLink?.Name != null && sub != null &&
            sub.CameFromDeepLinks.Contains(chatJoinRequest.InviteLink.Name) &&
            sub.PendingRequestToChannels.Contains(chatJoinRequest.Chat.Id))
        {
            // we already have info about the user, let's ignore
            return;
        }

        if (sub == null)
        {
            sub = InitSub(chatJoinRequest);
        }

        // update channel join status
        sub.PendingRequestToChannels.Add(chatJoinRequest.Chat.Id);
        if (chatJoinRequest.InviteLink?.Name != null) 
            sub.CameFromDeepLinks.Add(chatJoinRequest.InviteLink?.Name);

        if (_welcomeBotSettings.RequestsApproveMode == WelcomeBotSettings.BotRequestsApproveMode.Deffered)
        {
            // just save user
            await _subscribersDatabase.UpdateSubscriber(sub);
        }
        else
        {
            // lets do something
            await ApproveUserJoinAndSaveSub(sub, chatJoinRequest.Chat.Id);
        }
    }

    public async Task ApproveUserJoinAndSaveSub(SubscriberDto sub, long chatId)
    {
        await _welcomeBotSettings.LoadDefaultIfNeeded();
        var joined = false;
        Message captchaSent = null;
        var blockedByUser = false;
        try
        {
            joined = await _botClient.ApproveChatJoinRequest(chatId, sub.Id);
            if (_welcomeBotSettings.CaptchaMessage != null && sub.CaptchaStatus != CaptchaStatus.Passed)
            {
                captchaSent = await _massMessageSendingFactory.CreateDefault().EnqueueMessage(
                    new MassMessageSendingService.MessageRequest(_welcomeBotSettings.CaptchaMessage, sub.Id));
            }
        }
        catch (Exception e)
        {
            if (e.Message.Contains("USER_ALREADY_PARTICIPANT"))
            {
                joined = true;
            }
            else if (e.Message.Contains("Forbidden: bot was blocked by the user"))
            {
                blockedByUser = true;
            }
            else if (e.Message.Contains("bot can't initiate conversation with a user"))
            {
                blockedByUser = true;
            }
            else await _loggingChannel.LogExceptionToServiceChannel(
                "Помилка під час опрацьування прийому заявки від користувача. Це не вплине на користувача або на його підписку.",
                e);
        }

        if (blockedByUser)
        {
            sub.IsBotBlockedByUser = true;
        }
        
        if (joined)
        {
            sub.PendingRequestToChannels.Remove(chatId);
            sub.JoinedInChannels.Add(chatId);
        }

        if (captchaSent != null)
        {
            if (sub.MessagesHistory == null)
                sub.MessagesHistory = new List<MessageDetail>();
            
            sub.MessagesHistory.Add(new MessageDetail()
            {
                MessageId = captchaSent.MessageId,
                MessageType = MessageType.Captcha,
                SentTime = DateTime.UtcNow.ToFirestoreTimestamp()
            });
            sub.CaptchaStatus = CaptchaStatus.Sent;
        }

        await _subscribersDatabase.UpdateSubscriber(sub);
    }

    private SubscriberDto InitSub(ChatJoinRequest chatJoinRequest)
    {
        return new SubscriberDto()
        {
            Id = chatJoinRequest.From.Id,
            Language = chatJoinRequest.From.LanguageCode,
            UserName = chatJoinRequest.From.Username,
            LastName = chatJoinRequest.From.LastName,
            FirstName = chatJoinRequest.From.FirstName,
            RegistrationDate = DateTime.UtcNow.ToFirestoreTimestamp(),
            RegistrationSource = SubscriberCameFrom.ChannelJoinRequest
        };
    }
    
    public async Task<(ChannelSettingsDto channel, long? pendingRequestsCount)[]> GetPendingRequestsByChannels()
    {
        await _channelsSettings.LoadSchedule();

        var res = await Task.WhenAll(_channelsSettings.ChannelSettings
            .Select(async c => (channel: c, pendingRequestsCount: await _subscribersDatabase.PendingRequestsCount(c.ChannelId))));

        return res;
    }

    public async Task UpdateUserStatus(ChatMemberUpdated myChatMember)
    {
        await Task.Delay(1000);
        var sub = await _subscribersDatabase.GetSubscriber(myChatMember.Chat.Id);
        if (sub != null)
        {
            if (myChatMember.OldChatMember.Status != ChatMemberStatus.Kicked &&
                myChatMember.NewChatMember.Status == ChatMemberStatus.Kicked)
            {
                sub.IsBotBlockedByUser = true; // :(
            }

            if (myChatMember.OldChatMember.Status == ChatMemberStatus.Kicked &&
                myChatMember.NewChatMember.Status != ChatMemberStatus.Kicked)
            {
                sub.IsBotBlockedByUser = false; // :)
            }

            await _subscribersDatabase.UpdateSubscriber(sub);
        }
    }
}