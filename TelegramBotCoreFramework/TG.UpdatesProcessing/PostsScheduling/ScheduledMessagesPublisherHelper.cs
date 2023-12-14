using CommunicationChat.MassSendings;
using Google.Rpc;
using Helpers;
using Helpers.Extensions;
using Helpers.PredefinedChannels;
using Helpers.UserAuth;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TG.UpdatesProcessing.BotCommands;
using TG.UpdatesProcessing.BotCommands.UserAuth;
using TG.UpdatesProcessing.PostsScheduling;
using TL;
using InputMediaDocument = Telegram.Bot.Types.InputMediaDocument;
using InputMediaPhoto = Telegram.Bot.Types.InputMediaPhoto;
using Message = TL.Message;

namespace TG.UpdatesProcessing;

public class ScheduledMessagesPublisherHelper
{
    private readonly ChannelsSettings _channelsSettings;
    private readonly ProjectTeamCommunication _projectTeamCommunication;
    private readonly ScheduledMessagesSettings _scheduledMessagesSettings;
    private readonly ScheduledMessagesArchive _scheduledMessagesArchive;
    private readonly TelegramBotClient _telegramBotClient;
    private readonly TgUserAuthController _tgUserAuthController;
    private readonly ChannelsInfoParser _channelsInfoParser;
    private readonly LoggingChannel _loggingChannel;
    private readonly MassMessagesDeletingFactory _massMessagesDeletingFactory;
    private readonly MassMessageSendingFactory _massMessageSendingFactory;

    public ScheduledMessagesPublisherHelper(ChannelsSettings channelsSettings,
        ProjectTeamCommunication projectTeamCommunication,
        ScheduledMessagesSettings scheduledMessagesSettings,
        ScheduledMessagesArchive scheduledMessagesArchive,
        TelegramBotClient telegramBotClient,
        TgUserAuthController tgUserAuthController,
        ChannelsInfoParser channelsInfoParser,
        LoggingChannel loggingChannel,
        MassMessagesDeletingFactory massMessagesDeletingFactory,
        MassMessageSendingFactory massMessageSendingFactory)
    {
        _channelsSettings = channelsSettings;
        _projectTeamCommunication = projectTeamCommunication;
        _scheduledMessagesSettings = scheduledMessagesSettings;
        _scheduledMessagesArchive = scheduledMessagesArchive;
        _telegramBotClient = telegramBotClient;
        _tgUserAuthController = tgUserAuthController;
        _channelsInfoParser = channelsInfoParser;
        _loggingChannel = loggingChannel;
        _massMessagesDeletingFactory = massMessagesDeletingFactory;
        _massMessageSendingFactory = massMessageSendingFactory;
    }

    public async Task NotifyAdminsMessageSent(ScheduledMessage scheduledMessage)
    {
        await _channelsSettings.LoadSchedule();
        var infos = scheduledMessage.SentMessages
            .Select( c => new
            {
                c.ChatId,
                messageId = c.MessageId,
                channel = _channelsSettings.ChannelSettings.First(i=>i.ChannelId == c.ChatId)
            });

        var publishDate = scheduledMessage.PublishDate;
        var publishingLifeTime = Math.Round(scheduledMessage.PublishLifetimeMinutes / 60.0, 1);
        var conditions = publishingLifeTime > 0
            ? $"{publishingLifeTime} годин(и) в стрічці, буде видалено {publishDate.ToDateTime().UtcToUaTime().AddMinutes(scheduledMessage.PublishLifetimeMinutes):yyyy-MM-dd HH:mm}"
            : "без видалення";

        var urls = string.Join(",\n ",
            infos.Select(i => i.channel.GetHtmlUrl(messageId: i.messageId)));
        var messagePreview = scheduledMessage?.Message?.Caption ?? scheduledMessage?.Message.Text ?? "повідомлення без тексту";
        messagePreview = string.Concat(messagePreview.AsSpan(0, Math.Min(30, messagePreview.Length)), "..."); 
        var text = $"Запланований пост <pre>\"{messagePreview}\"</pre> успішно опублікований 🥳\n" +
                   $"Умови розміщення: {conditions}\n" +
                   $"Посилання:\n {urls}\n" +
                   "\n👁👃👁";
        await _projectTeamCommunication.SendMessageToAllManagers(text);
    }

    public async Task MarkWaitingToRemovalMessage(ScheduledMessage message, (long chatId, int messageId)[] sent)
    {
        message.State = ScheduledMessageState.WaitingForRemoval;
        message.SentMessages = sent
            .Select(s => new PublishedMessageInfo() { ChatId = s.chatId, MessageId = s.messageId }).ToArray();

        await _scheduledMessagesSettings.UpdateMessage(message);
    }
    
    public Task ArchiveMessage(ScheduledMessage message, (long chatId, int messageId)[] sent)
    {
        message.SentMessages = sent.Select(s => new PublishedMessageInfo()
            { ChatId = s.chatId, MessageId = s.messageId }).ToArray();
        return ArchiveMessage(message);
    }
    
    public async Task ArchiveMessage(ScheduledMessage message)
    {
        var archiveMessage = new ScheduledMessageArchive(message)
        {
            ArchiveTime = DateTime.UtcNow.ToFirestoreTimestamp()
        };
        await _scheduledMessagesArchive.AddMessage(archiveMessage);
        await _scheduledMessagesSettings.RemoveMessage(message.Index.ToString());
    }


    public async Task NotifyAdminsMessageRemovedAndUpdateViews(ScheduledMessage message)
    {
        await _channelsSettings.LoadSchedule();
        var ReportMessage = "";
        if (_tgUserAuthController.IsLoggedIn())
        {
            var totalViews = 0;
            var totalReactions = 0;
            var totalForwards = 0;

            var channels = await _channelsInfoParser.GetChannelsListForAnalysing();
            var messagePreview = message?.Message?.Caption ?? message?.Message.Text ?? "повідомлення без тексту";
            messagePreview = string.Concat(messagePreview.AsSpan(0, Math.Min(30, messagePreview.Length)), "...");
            ReportMessage = $"Пост <pre>\"{messagePreview}\"</pre> видалено!\n\nРезультати по каналам:\n";
            for (var i = 0; i < message.SentMessages.Length; i++)
            {
                var sent = message.SentMessages[i];
                var channel = channels.FirstOrDefault(c => c.id == sent.ChatId * -1 - 1000000000000);
                var channelSettings = _channelsSettings.ChannelSettings.First(c => c.ChannelId == sent.ChatId);
                var publishedMessages = await _tgUserAuthController.UserClient.Channels_GetMessages(channel, sent.MessageId);
                var pMessage = publishedMessages.Messages.First() as Message;
                totalViews += pMessage.views;
                var reactionsCount = pMessage.reactions?.results.Sum(r => r.count) ?? 0;
                totalReactions += reactionsCount + pMessage.forwards;
                totalForwards += pMessage.forwards;
                sent.Views = pMessage.views;
                sent.Reactions = reactionsCount + pMessage.forwards;

                ReportMessage += $"> {channelSettings.GetHtmlUrl()}\n";
                var reactions = reactionsCount > 0
                    ? string.Join(", ",
                        pMessage.reactions.results.Select(r => $"{(r.reaction as ReactionEmoji).emoticon}:{r.count}"))
                    : "-";
                ReportMessage +=
                    $"👀 {pMessage.views} | Er {(reactionsCount + pMessage.forwards) * 100f / pMessage.views:0.##}% | Forwards: {pMessage.forwards} | Reactions: {reactions}\n";

            }

            ReportMessage += "\nЗагалом:";
            ReportMessage += $"\nПереглядів: {totalViews}";
            ReportMessage += $"\nПересилань: {totalForwards}";
            ReportMessage += $"\nРеакцій: {totalReactions}";
            ReportMessage += $"\nER: {totalReactions * 100f / totalViews:0.##}%";
            ReportMessage += "\n\nДякуємо за розміщення та розраховуємо на майбутню співпрацю! 👨‍💻";
        }
        else
        {
            await _projectTeamCommunication
                .SendMessageToAllOwners(
                    "Нажаль, телеграм обмежує для ботів інформацію про перегляди і реакції на повідомлення. " +
                    "Для підрахунку цих даних потрібен акаунт \"живої\" людини. " +
                    "Якщо в майбутньому ви хочете отримувати детальну інформацію по розміщунню - авторизуйте додатковий акаунт в налаштуваннях бота.");
            
            
            var messagePreview = message?.Message?.Caption ?? message?.Message.Text ?? "повідомлення без тексту";
            messagePreview = string.Concat(messagePreview.AsSpan(0, Math.Min(30, messagePreview.Length)), "...");
            ReportMessage = $"Пост <pre>\"{messagePreview}\"</pre> видалено із каналів:\n";
            for (var i = 0; i < message.SentMessages.Length; i++)
            {
                var sent = message.SentMessages[i];
                var channelSettings = _channelsSettings.ChannelSettings.First(c => c.ChannelId == sent.ChatId);
                ReportMessage += $"> {channelSettings.GetHtmlUrl()}\n";
            }
            ReportMessage += "\n\nДякуємо за розміщення та розраховуємо на майбутню співпрацю! 👨‍💻";
        }

        var massDeleting = _massMessagesDeletingFactory.CreateDefault();
        await Task.WhenAll(message.SentMessages.Select(async sent =>
        {
            try
            {
                await massDeleting.EnqueueMessage((sent.ChatId, sent.MessageId));
            }
            catch (Exception e)
            {
                var url = _channelsSettings.ChannelSettings.First(c => c.ChannelId == sent.ChatId)?.GetHtmlUrl(messageId: sent.MessageId) ?? "<unknown>";
                await _loggingChannel.LogExceptionToServiceChannel($"Помилка під час видалення планового поста. Вона не скажеться на роботі бота (але перевірте що повідомлення {url} видалено).",e);
            }
        }));
        await _projectTeamCommunication.SendMessageToAllManagers(ReportMessage);
    }

    public async Task<(long chatId, int messageId)[]> SendMessage(ScheduledMessage message)
    {
        var sender = _massMessageSendingFactory.CreateDefault();
        var tasks = message.ChatIdsToSend
            .Select(chatId => sender.EnqueueMessage(new MassMessageSendingService.MessageRequest(message.Message, chatId)));
        var posts = await Task.WhenAll(tasks);
        return posts.Select(p => (p.Chat.Id, p.MessageId)).ToArray();
    }

    public async Task<string> GetDetailedPublicationAnalytics(ScheduledMessage message)
    {
        var ReportMessage = "";
        var totalViews = 0;
        var totalReactions = 0;
        var totalForwards = 0;

        var channels = await _channelsInfoParser.GetChannelsListForAnalysing();
        var messagePreview = message?.Message?.Caption ?? message?.Message.Text ?? "повідомлення без тексту";
        messagePreview = string.Concat(messagePreview.AsSpan(0, Math.Min(30, messagePreview.Length)), "...");
        ReportMessage = $"Поточна статистика розміщення поста <pre>\"{messagePreview}\"</pre> по каналам:\n";
        for (var i = 0; i < message.SentMessages.Length; i++)
        {
            var sent = message.SentMessages[i];
            var channel = channels.FirstOrDefault(c => c.id == sent.ChatId * -1 - 1000000000000);
            var channelSettings = _channelsSettings.ChannelSettings.First(c => c.ChannelId == sent.ChatId);
            var publishedMessages = await _tgUserAuthController.UserClient.Channels_GetMessages(channel, sent.MessageId);
            var pMessage = publishedMessages.Messages.First() as Message;
            totalViews += pMessage.views;
            var reactionsCount = pMessage.reactions?.results.Sum(r => r.count) ?? 0;
            totalReactions += reactionsCount + pMessage.forwards;
            totalForwards += pMessage.forwards;
            sent.Views = pMessage.views;
            sent.Reactions = reactionsCount + pMessage.forwards;

            ReportMessage += $"> {channelSettings.GetHtmlUrl(messageId: message.SentMessages[i].MessageId)}\n";
            var reactions = reactionsCount > 0
                ? string.Join(", ",
                    pMessage.reactions.results.Select(r => $"{(r.reaction as ReactionEmoji).emoticon}:{r.count}"))
                : "-";
            ReportMessage +=
                $"👀 {pMessage.views} | Er {(reactionsCount + pMessage.forwards) * 100f / pMessage.views:0.##}% | Forwards: {pMessage.forwards} | Reactions: {reactions}\n";

        }

        ReportMessage += "\nЗагалом:";
        ReportMessage += $"\nПереглядів: {totalViews}";
        ReportMessage += $"\nПересилань: {totalForwards}";
        ReportMessage += $"\nРеакцій: {totalReactions}";
        ReportMessage += $"\nER: {totalReactions * 100f / totalViews:0.##}%";
        return ReportMessage;
    }

    public async Task TerminateFromPublication(ScheduledMessage message, string reason)
    {   
        try
        {
            await _projectTeamCommunication.SendMessageToAllManagers(reason);
            await NotifyAdminsMessageRemovedAndUpdateViews(message);
            await ArchiveMessage(message);
        }
        catch (Exception e)
        {
            message.AllowedToSend = false;
            await _scheduledMessagesSettings.UpdateMessage(message);
            await _loggingChannel.LogExceptionToServiceChannel($"Error while trying to terminate scheduled message {message.Index}", e);
        }
    }

    public async Task UpdateButtonsOnPublishedPosts(ScheduledMessage message)
    {
        foreach (var info in message.SentMessages)
        {
            await _telegramBotClient.EditMessageReplyMarkupAsync(info.ChatId, info.MessageId,
                message.Message.ReplyMarkup);
        }
    }
    
    
    public async Task UpdatePublishedPosts(ScheduledMessage message)
    {
        foreach (var info in message.SentMessages)
        {
            switch (message.Message.Type)
            {
                case MessageType.Text:
                    await _telegramBotClient.EditMessageTextAsync(
                        chatId: info.ChatId, 
                        messageId: info.MessageId,
                        text: message.Message.GetHTML(),
                        parseMode: ParseMode.Html,
                        disableWebPagePreview: true,
                        replyMarkup: message.Message.ReplyMarkup);
                    break;
                case MessageType.Photo:
                    await _telegramBotClient.EditMessageMediaAsync(
                        chatId: info.ChatId,
                        messageId: info.MessageId,
                        replyMarkup: message.Message.ReplyMarkup,
                        media: new InputMediaPhoto(new InputFileId(message.Message.Photo.Last().FileId))
                        {
                            ParseMode = ParseMode.Html,
                            Caption = message.Message.Caption,
                            HasSpoiler = message.Message.HasMediaSpoiler,
                            CaptionEntities = message.Message.CaptionEntities
                        });
                    break;
                case MessageType.Video:
                    await _telegramBotClient.EditMessageMediaAsync(
                        chatId: info.ChatId,
                        messageId: info.MessageId,
                        replyMarkup: message.Message.ReplyMarkup,
                        media: new InputMediaVideo(new InputFileId(message.Message.Video.FileId))
                        {
                            ParseMode = ParseMode.Html,
                            Caption = message.Message.Caption,
                            HasSpoiler = message.Message.HasMediaSpoiler,
                            CaptionEntities = message.Message.CaptionEntities,
                            Duration = message.Message.Video.Duration,
                            Height = message.Message.Video.Height,
                            Thumbnail = new InputFileId(message.Message.Video.Thumbnail.FileId),
                            Width = message.Message.Video.Width
                        });
                    break;
                case MessageType.Document:
                    await _telegramBotClient.EditMessageMediaAsync(
                        chatId: info.ChatId,
                        messageId: info.MessageId,
                        replyMarkup: message.Message.ReplyMarkup,
                        media: new InputMediaDocument(new InputFileId(message.Message.Document.FileId))
                        {
                            ParseMode = ParseMode.Html,
                            Caption = message.Message.Caption,
                            CaptionEntities = message.Message.CaptionEntities,
                            Thumbnail = new InputFileId(message.Message.Document.Thumbnail.FileId),
                        });
                    break;
                default: throw new Exception($"Unknown message type for repost: {message.Message.Type}");
            }
        }
    }
}