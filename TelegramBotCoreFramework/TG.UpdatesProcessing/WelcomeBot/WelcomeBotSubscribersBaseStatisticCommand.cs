using Analytics.UsersDatabase;
using Helpers.AdminsCommunication;
using Telegram.Bot;
using Telegram.Bot.Types;
using TG.UpdatesProcessing.BotCommands;
using TG.UpdatesProcessing.WelcomeBotPostsScheduling;

namespace TG.UpdatesProcessing.WelcomeBot;

public class WelcomeBotSubscribersBaseStatisticCommand : BotCommandControllerBase
{
    private readonly SubscribersDatabase _subscribersDatabase;
    private readonly WelcomeBotScheduledMessagesArchive _welcomeBotScheduledMessagesArchive;
    public override string CommandName => "🙆 Статистика бази";
    public override CommandsAccessLevel AccessLevel => CommandsAccessLevel.Owner;
    public override Type? ParentCommandType => typeof(WelcomeBotRootCommand);
    
    
    public WelcomeBotSubscribersBaseStatisticCommand(TelegramBotClient botClient,
        IBotCommandsFactory botCommandsFactory,
        AdminsController adminsController, SubscribersDatabase subscribersDatabase,
        WelcomeBotScheduledMessagesArchive welcomeBotScheduledMessagesArchive, AdminUsers adminUsers) 
        : base(botClient, botCommandsFactory, adminsController, adminUsers)
    {
        _subscribersDatabase = subscribersDatabase;
        _welcomeBotScheduledMessagesArchive = welcomeBotScheduledMessagesArchive;
    }
    
    protected override Task Build()
    {
        AddDefaultShortcut(DefaultHandler);
        return Task.CompletedTask;
    }

    private async Task<CommandResult> DefaultHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var stats = await _subscribersDatabase.GetStats();
        var archieve = await _welcomeBotScheduledMessagesArchive.GetAllScheduledMessages();

        var messagesDeliveredTotal =
            archieve.Sum(a => a.DeliveryReport?.DeliveredMessages ?? 0 + a.DeliveryReport?.BlockedByUser ?? 0);
        var deliveringTimeSec = archieve.Where(a => a.DeliveryReport != null).Sum(a =>
            (a.DeliveryReport.EndTime.ToDateTime() - a.DeliveryReport.StartTime.ToDateTime()).TotalSeconds);
        var messagesCleanedUpTotal =
            archieve.Sum(a => a.CleanupReport?.CleanedMessages ?? 0 + a.CleanupReport?.Errors ?? 0);
        var cleaningTimeSec = archieve.Where(a => a.CleanupReport != null)
            .Sum(a =>
                (a.CleanupReport.EndTime.ToDateTime() - a.CleanupReport.StartTime.ToDateTime()).TotalSeconds);

        var txt = $"Поточна статистика по базі користувачів:\n" +
                  $"Всього користувачів: {stats.AllSubscribersInDb}\n" +
                  $"Користувачів у вітальному боті: {stats.SubsInWelcomeBot}\n" +
                  $"\n" +
                  $"Вітальний бот\n" +
                  $"Аткивних користувачів: {stats.ActiveUsers}\n" +
                  $"Капча відправлена: {stats.CaptchaSent}\n" +
                  $"Конверсія капчі {stats.CaptchaConversion * 100:F1}%\n" +
                  $"Ще не отримували розсилок: {stats.NotReceivedAds}\n" +
                  $"Заблокували бота: {stats.BlockedBot}\n" +
                  $"\n" +
                  $"Завершених розсилок: {archieve.Length}\n" +
                  $"Відправлено повідомлень: {messagesDeliveredTotal}\n" +
                  $"Доставлено повідомлень: {archieve.Sum(a => a.DeliveryReport?.DeliveredMessages ?? 0)}\n" +
                  $"Зачищено повідомлень: {messagesCleanedUpTotal}\n" +
                  $"Середня швидкість відправки: {messagesDeliveredTotal / deliveringTimeSec:F1} повідомлень/сек.\n" +
                  $"Середня швидкість видалення: {messagesCleanedUpTotal / cleaningTimeSec:F1} повідомлень/сек.\n" +
                  $"\n\nДля оптимізації роботи з даними, статистика по підписникам оновлюється не частіне ніж раз на день.";
        await ComposeMessage(update)
            .SetText(txt)
            .SetNeedUpMenuButton()
            .SetNeedMainMenuButton()
            .Send();

        return CommandResult.Ok;
    }
}