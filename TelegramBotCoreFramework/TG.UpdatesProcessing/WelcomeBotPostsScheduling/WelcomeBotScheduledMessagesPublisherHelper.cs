using Analytics.UsersDatabase;
using CommunicationChat.MassSendings;
using Google.Cloud.Firestore;
using Helpers.Extensions;
using Helpers.PredefinedChannels;
using Helpers.UserAuth;
using Telegram.Bot;
using TG.UpdatesProcessing.BotCommands;
using TG.UpdatesProcessing.WelcomeBot;
using TL;

namespace TG.UpdatesProcessing.WelcomeBotPostsScheduling;

public class WelcomeBotScheduledMessagesPublisherHelper
{
    private readonly ProjectTeamCommunication _projectTeamCommunication;
    private readonly WelcomeBotScheduledMessagesSettings _scheduledMessagesSettings;
    private readonly WelcomeBotScheduledMessagesArchive _scheduledMessagesArchive;
    private readonly LoggingChannel _loggingChannel;
    private readonly SubscribersDatabase _subscribersDatabase;
    private readonly MassMessageSendingFactory _massMessageSendingFactory;
    private readonly MassMessagesDeletingFactory _massMessagesDeletingFactory;

    public WelcomeBotScheduledMessagesPublisherHelper(
        ProjectTeamCommunication projectTeamCommunication,
        WelcomeBotScheduledMessagesSettings scheduledMessagesSettings,
        WelcomeBotScheduledMessagesArchive scheduledMessagesArchive,
        LoggingChannel loggingChannel,
        SubscribersDatabase subscribersDatabase, 
        MassMessageSendingFactory massMessageSendingFactory,
        MassMessagesDeletingFactory massMessagesDeletingFactory)
    {
        _projectTeamCommunication = projectTeamCommunication;
        _scheduledMessagesSettings = scheduledMessagesSettings;
        _scheduledMessagesArchive = scheduledMessagesArchive;
        _loggingChannel = loggingChannel;
        _subscribersDatabase = subscribersDatabase;
        _massMessageSendingFactory = massMessageSendingFactory;
        _massMessagesDeletingFactory = massMessagesDeletingFactory;
    }

    public async Task NotifyAdminsMessageStartSending(WelcomeBotScheduledMessage message)
    {
        var messagePreview = message?.Message?.Caption ?? message?.Message.Text ?? "повідомлення без тексту";
        messagePreview = string.Concat(messagePreview.AsSpan(0, Math.Min(30, messagePreview.Length)), "..."); 
        var text = $"Розпочата планова розсилка поста <pre>\"{messagePreview}\"</pre> 🥳\n\n" +
                   $"Цільова кількість розсилки: {message.TargetDeliveryAmount}\n" +
                   $"Очікуйте найближчим часом повідомлення про результати розсилки\n" +
                   "\n🕗🕛🕔🕧";
        await _projectTeamCommunication.SendMessageToAllManagers(text);
    }
    
    public async Task NotifyAdminsMessageSent(WelcomeBotScheduledMessage scheduledMessage,
        DeliveryWelcomeBotMessageReport report)
    {
        var publishDate = scheduledMessage.PublishDate;
        var publishingLifeTime = Math.Round(scheduledMessage.PublishLifetimeMinutes / 60.0, 1);
        var conditions = publishingLifeTime > 0
            ? $"{publishingLifeTime} годин(и) розміщення, буде видалено {publishDate.ToDateTime().UtcToUaTime().AddMinutes(scheduledMessage.PublishLifetimeMinutes):yyyy-MM-dd HH:mm}"
            : "без видалення";

        var deliverySpeed = (report.DeliveredMessages + report.BlockedByUser)/(report.EndTime.ToDateTime() - report.StartTime.ToDateTime()).TotalSeconds;
        var messagePreview = scheduledMessage?.Message?.Caption ?? scheduledMessage?.Message.Text ?? "повідомлення без тексту";
        messagePreview = string.Concat(messagePreview.AsSpan(0, Math.Min(30, messagePreview.Length)), "..."); 
        var text = $"Планова розсилка поста <pre>\"{messagePreview}\"</pre> завершилась успішно! 🥳\n\n" +
                   $"Початок розсилки: {report.StartTime.ToDateTime().UtcToUaTime():yyyy-MM-dd HH:mm}\n" +
                   $"Кінець розсилки: {report.EndTime.ToDateTime().UtcToUaTime():yyyy-MM-dd HH:mm}\n" +
                   $"Швидкість доставки: {deliverySpeed:F1} повідомлень/сек.\n" +
                   $"Кількість доставлених повідомлень: {report.DeliveredMessages}\n" +
                   $"Кількість знайдених блокувань бота: {report.BlockedByUser}\n" +
                   $"Умови розміщення: {conditions}\n" +
                   "\n👁👃👁";
        await _projectTeamCommunication.SendMessageToAllManagers(text);
    }

    public async Task MarkWaitingToRemovalMessage(WelcomeBotScheduledMessage message, DeliveryWelcomeBotMessageReport report)
    {
        message.DeliveryReport = report;
        message.State = WelcomeBotScheduledMessageState.WaitingForRemoval;

        await _scheduledMessagesSettings.UpdateMessage(message);
    }
    
    public Task ArchiveMessage(WelcomeBotScheduledMessage message, DeliveryWelcomeBotMessageReport report)
    {
        message.DeliveryReport = report;
        return ArchiveMessage(message);
    }
    
    public Task ArchiveMessage(WelcomeBotScheduledMessage message, CleanupWelcomeBotMessageReport report)
    {
        message.CleanupReport = report;
        return ArchiveMessage(message);
    }
    
    private async Task ArchiveMessage(WelcomeBotScheduledMessage message)
    {
        var archiveMessage = new WelcomeBotScheduledMessageArchive(message)
        {
            ArchiveTime = DateTime.UtcNow.ToFirestoreTimestamp()
        };
        await _scheduledMessagesArchive.AddMessage(archiveMessage);
        await _scheduledMessagesSettings.RemoveMessage(message.Index.ToString());
    }


    public async Task NotifyAdminsMessageCleanup(WelcomeBotScheduledMessage scheduledMessage,
        CleanupWelcomeBotMessageReport report)
    {
        var deliverySpeed = (report.CleanedMessages + report.Errors)/(report.EndTime.ToDateTime() - report.StartTime.ToDateTime()).TotalSeconds;
        var messagePreview = scheduledMessage?.Message?.Caption ?? scheduledMessage?.Message.Text ?? "повідомлення без тексту";
        messagePreview = string.Concat(messagePreview.AsSpan(0, Math.Min(30, messagePreview.Length)), "..."); 
        var text = $"Пост <pre>\"{messagePreview}\"</pre> видалено!\n\n" +
                   $"Початок видалення: {report.StartTime.ToDateTime().UtcToUaTime():yyyy-MM-dd HH:mm}\n" +
                   $"Кінець видалення: {report.EndTime.ToDateTime().UtcToUaTime():yyyy-MM-dd HH:mm}\n" +
                   $"Швидкість видалення: {deliverySpeed:F1} повідомлень/сек.\n" +
                   $"Кількість видалених повідомлень: {report.CleanedMessages}\n" +
                   $"Кількість не успішних видалень: {report.Errors}\n" +
                   "\nДякуємо за розміщення та розраховуємо на майбутню співпрацю! 👨‍💻";
        
        await _projectTeamCommunication.SendMessageToAllManagers(text);
    }

    public async Task<DeliveryWelcomeBotMessageReport> SendMessage(WelcomeBotScheduledMessage message)
    {
        message.State = WelcomeBotScheduledMessageState.Delivering;
        await _scheduledMessagesSettings.UpdateMessage(message);
        var report = new DeliveryWelcomeBotMessageReport
        {
            StartTime = DateTime.UtcNow.ToFirestoreTimestamp()
        };

        var usersToSend = new List<long>();
        int limit = 50;
        var query = _subscribersDatabase.GetAvailableToSendSubsQuery()
            .Limit(limit);
        DocumentSnapshot lastDocument = null;
        while (true)
        {
            var queryWithCursor = lastDocument is null ? query : query.StartAfter(lastDocument);
            var querySnapshot = await queryWithCursor.GetSnapshotAsync();
            var tasks = querySnapshot.Select(async document =>
            {
                var sub = document.ConvertTo<SubscriberDto>();
                try
                {
                    var m = await _massMessageSendingFactory.CreateDefault().EnqueueMessage(
                        new MassMessageSendingService.MessageRequest(message.Message, sub.Id));
                    if (sub.MessagesHistory == null)
                        sub.MessagesHistory = new List<MessageDetail>();
                    sub.MessagesHistory.Add(new MessageDetail()
                    {
                        ScheduledMessageId = message.Index,
                        MessageId = m.MessageId,
                        MessageType = MessageType.Advertisement,
                        SentTime = DateTime.UtcNow.ToFirestoreTimestamp()
                    });
                    sub.LastDelivery = DateTime.UtcNow.ToFirestoreTimestamp();
                    sub.DeliveredAdMessagesCount++;
                    sub.PlacedNowMessages.Add(message.Index);
                    lock (report)
                    {
                        report.DeliveredMessages++;
                    }
                }
                catch (Exception e)
                {
                    sub.IsBotBlockedByUser = true;
                    lock (report)
                    {
                        report.BlockedByUser++;
                    }

                    if (e.Message.Contains("Forbidden: bot was blocked by the user"))
                    {
                        // ignore
                    }
                    else if (e.Message.Contains("bot can't initiate conversation with a user"))
                    {
                        // ignore
                    }
                    else if (e.Message.Contains("user is deactivated"))
                    {
                        // ignore
                    }
                    else
                    {
                        await _loggingChannel.LogExceptionToServiceChannel(
                            $"Помилка під час масової відправки повідомлень. Повідомлення №{message.Index}, користувач {sub.Id}.",
                            e);
                    }
                }

                await _subscribersDatabase.UpdateSubscriber(sub);
            });
            await Task.WhenAll(tasks);

            if (querySnapshot.Count != limit || usersToSend.Count>=message.TargetDeliveryAmount)
            {
                break;
            }
            lastDocument = querySnapshot.Documents.LastOrDefault();
        }
        
        report.EndTime = DateTime.UtcNow.ToFirestoreTimestamp();
        return report;
    }

    public async Task<string> GetDetailedPublicationAnalytics(WelcomeBotScheduledMessage message)
    {
        var report = message.DeliveryReport;
        if (report == null)
            return "На даний момент не доступна інформація по розміщену поста.";
        var publishDate = message.PublishDate;
        var publishingLifeTime = Math.Round(message.PublishLifetimeMinutes / 60.0, 1);
        var conditions = publishingLifeTime > 0
            ? $"{publishingLifeTime} годин(и) розміщення, буде видалено {publishDate.ToDateTime().UtcToUaTime().AddMinutes(message.PublishLifetimeMinutes):yyyy-MM-dd HH:mm}"
            : "без видалення";

        var deliverySpeed = (report.DeliveredMessages + report.BlockedByUser)/(report.EndTime.ToDateTime() - report.StartTime.ToDateTime()).TotalSeconds;
        var messagePreview = message?.Message?.Caption ?? message?.Message.Text ?? "повідомлення без тексту";
        messagePreview = string.Concat(messagePreview.AsSpan(0, Math.Min(30, messagePreview.Length)), "..."); 
        var text = $"Планова розсилка поста <pre>\"{messagePreview}\"</pre> завершилась успішно! 🥳\n" +
                   $"Початок розсилки: {report.StartTime.ToDateTime().UtcToUaTime():yyyy-MM-dd HH:mm}\n" +
                   $"Кінець розсилки: {report.EndTime.ToDateTime().UtcToUaTime():yyyy-MM-dd HH:mm}\n" +
                   $"Швидкість доставки: {deliverySpeed:F1} повідомлень/сек.\n" +
                   $"Кількість доставлених повідомлень: {report.DeliveredMessages}\n" +
                   $"Кількість знайдених блокувань бота: {report.BlockedByUser}\n" +
                   $"Умови розміщення: {conditions}\n" +
                   "\n👁👃👁";
        return text;
    }

    public async Task TerminateFromPublication(WelcomeBotScheduledMessage message, string reason)
    {   
        try
        {
            await _projectTeamCommunication.SendMessageToAllManagers(reason);
            var report = await CleanupMessage(message);
            await NotifyAdminsMessageCleanup(message, report);
            await ArchiveMessage(message, report);
        }
        catch (Exception e)
        {
            message.AllowedToSend = false;
            await _scheduledMessagesSettings.UpdateMessage(message);
            await _loggingChannel.LogExceptionToServiceChannel($"Error while trying to terminate scheduled delivery {message.Index}", e);
        }
    }

    public async Task<CleanupWelcomeBotMessageReport> CleanupMessage(WelcomeBotScheduledMessage message)
    {
        message.State = WelcomeBotScheduledMessageState.Cleaning;
        await _scheduledMessagesSettings.UpdateMessage(message);
        var report = new CleanupWelcomeBotMessageReport
        {
            StartTime = DateTime.UtcNow.ToFirestoreTimestamp()
        };
        
        int limit = 100;

        while (true)
        {
            var subsQuery = _subscribersDatabase.GetSubsWithPlacedMessageIdQuery(message.Index);
            var query = subsQuery.Limit(limit);
            var querySnapshot = await query.GetSnapshotAsync();
            
            if (querySnapshot.Count == 0)
            {
                break;
            }
            
            var tasks = querySnapshot.Select(async document =>
            {
                var sub = document.ConvertTo<SubscriberDto>();
                if (sub.IsBotBlockedByUser)
                {
                    var deleted = sub.MessagesHistory?.RemoveAll(d => d.ScheduledMessageId == message.Index) ?? 0;
                    sub.PlacedNowMessages.RemoveAll(i => i == message.Index);
                    report.Errors += deleted;
                    await _subscribersDatabase.UpdateSubscriber(sub);
                    return;
                }
                
                var placingInfo = sub.MessagesHistory.FirstOrDefault(m => m.ScheduledMessageId == message.Index);

                try
                {
                    if (placingInfo == null)
                    {
                        report.Errors++;
                        await _loggingChannel.LogMessageToServiceChannel(
                            $"Не знайдено інформації про розміщене повідомлення під час видалення розсилки у користувача. Повідомлення №{message.Index}, користувач {sub.Id}.");
                    }
                    else
                    {
                        sub.MessagesHistory.Remove(placingInfo);
                    }
                    sub.PlacedNowMessages.Remove(message.Index);
                    await _massMessagesDeletingFactory.CreateDefault().EnqueueMessage((sub.Id, placingInfo.MessageId));
                    report.CleanedMessages++;
                }
                catch (Exception e)
                {
                    if (e.Message.Contains("Forbidden: bot was blocked by the user"))
                    {
                        sub.IsBotBlockedByUser = true;
                        report.Errors++;
                    }
                    else if (e.Message.Contains("bot can't initiate conversation with a user"))
                    {
                        sub.IsBotBlockedByUser = true;
                        report.Errors++;
                    }
                    else if (e.Message.Contains("message to delete not found"))
                    {
                        sub.IsBotBlockedByUser = true;
                        report.Errors++;
                    }
                    else
                    {
                        report.Errors++;
                        await _loggingChannel.LogExceptionToServiceChannel(
                            $"Помилка під час масової зачистки повідомлень. Повідомлення №{message.Index}, користувач {sub.Id}.",
                            e);
                    }
                }
                await _subscribersDatabase.UpdateSubscriber(sub);
            });
            await Task.WhenAll(tasks);
        }
        report.EndTime = DateTime.UtcNow.ToFirestoreTimestamp();
        return report;
    }
}