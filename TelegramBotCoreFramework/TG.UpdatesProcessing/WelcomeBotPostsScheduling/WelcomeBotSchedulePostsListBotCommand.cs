using System.Globalization;
using System.Text.RegularExpressions;
using Analytics.UsersDatabase;
using Helpers;
using Helpers.AdminsCommunication;
using Helpers.Extensions;
using Helpers.PredefinedChannels;
using Helpers.UserAuth;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TG.UpdatesProcessing.BotCommands;

namespace TG.UpdatesProcessing.WelcomeBotPostsScheduling;

public class WelcomeBotSchedulePostsListBotCommand : BotCommandControllerBase
{
    public override string CommandName => "📔 Заплановані розсилки";
    public override CommandsAccessLevel AccessLevel => CommandsAccessLevel.Manager;
    public override Type? ParentCommandType { get; } = typeof(WelcomeBotSchedulePostCommand);
    
    private readonly IUserInputAwaiting _userInputAwaiting;
    private readonly WelcomeBotScheduledMessagesSettings _scheduledMessagesSettings;
    private readonly ProjectTeamCommunication _projectTeamCommunication;
    private readonly TgUserAuthController _tgUserAuthController;
    private readonly WelcomeBotScheduledMessagesPublisherHelper _scheduledMessagesPublisherHelper;
    private readonly SubscribersDatabase _subscribersDatabase;
    private readonly AdminUsers _adminUsers;

    public WelcomeBotSchedulePostsListBotCommand(TelegramBotClient botClient, IBotCommandsFactory botCommandsFactory,
        IUserInputAwaiting userInputAwaiting,
        WelcomeBotScheduledMessagesSettings scheduledMessagesSettings,
        ProjectTeamCommunication projectTeamCommunication,
        TgUserAuthController tgUserAuthController,
        WelcomeBotScheduledMessagesPublisherHelper scheduledMessagesPublisherHelper,
        AdminsController adminsController,
        SubscribersDatabase subscribersDatabase, AdminUsers adminUsers) 
        : base(botClient, botCommandsFactory, adminsController, adminUsers)
    {
        _userInputAwaiting = userInputAwaiting;
        _scheduledMessagesSettings = scheduledMessagesSettings;
        _projectTeamCommunication = projectTeamCommunication;
        _tgUserAuthController = tgUserAuthController;
        _scheduledMessagesPublisherHelper = scheduledMessagesPublisherHelper;
        _subscribersDatabase = subscribersDatabase;
        _adminUsers = adminUsers;
    }

    protected override async Task Build()
    {
        AddDefaultShortcut(DrawListHandler);
        
        // editing
        AddArgShortcut("get", DrawMessageHandler);
        AddArgShortcut("edate", EditPostDateHandler);
        AddArgShortcut("edate+", EditPostDateApprovedHandler);
        AddArgShortcut("elifetime", EditPostLifeTimeHandler);
        AddArgShortcut("elifetime+", EditPostLifeTimeApprovedHandler);
        AddArgShortcut("abutton", AddButtonHandler);
        AddArgShortcut("abutton+", AddButtonApprovedHandler);
        AddArgShortcut("rbutton", RemoveAllButtonsHandler);
        AddArgShortcut("rbutton+", RemoveAllButtonsApprovedHandler);
        AddArgShortcut("tcount", EditTargetDeliveryCountHandler);
        AddArgShortcut("tcount+", EditTargetDeliveryCountApprovedHandler);
        AddArgShortcut("rpost", RemovePostHandler);
        AddArgShortcut("rpost+", RemovePostApprovedHandler);
        AddArgShortcut("chpost", ChangePostHandler);
        AddArgShortcut("chpost+", ChangePostApprovedHandler);
        AddArgShortcut("approve", ApproveSendHandler);
        
        //published
        AddArgShortcut("unpublish", UnPublishHandler);
        AddArgShortcut("unpublish+", UnPublishApprovedHandler);
        AddArgShortcut("current_stats", GetPublishStatusHandler);
    }
    
    private async Task<CommandResult> UnPublishHandler(Update update, string[]? args, string? reroutedForPath)
    {   
        var txt = $"Ви впевнені що хочете <b>зняти повідомлення #{args[1]} з публікації</b>?";
        await PromptUserDialog(update, txt, $"{MyPath}?unpublish+/{args[1]}",$"{MyPath}?get/{args[1]}");
        return CommandResult.Ok;
    }
    
    private async Task<CommandResult> UnPublishApprovedHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var me = _adminUsers.GetUser(update.GetChatId());
        if (me.BotAccessLevel != CommandsAccessLevel.Owner)
        {
            await ComposeMessage(update)
                .SetText("Ця функція доступна тільки власникам бота (акаунти із статусом Owner). " +
                         "Зверніться до власника, або в сервіс підтримки.")
                .SetNeedCurrentMenuButton()
                .SetNeedUpMenuButton()
                .Send();
        }
        else
        {
            var mIndex = args[1];
            var message = await _scheduledMessagesSettings.GetMessage(mIndex);
            Task.Run(async () => await _scheduledMessagesPublisherHelper.TerminateFromPublication(message,
                $"Користувач {me.DisplayName} (id {me.UserId}) вручну зняв із публікації пост {message.Index} із бота"));
        }
        return CommandResult.Ok;
    }
    
    private async Task<CommandResult> DrawMessageHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var mIndex = args[1];
        var message = await _scheduledMessagesSettings.GetMessage(mIndex);

        if (message.Message == null)
        {
            await _scheduledMessagesSettings.RemoveMessage(mIndex);
            return await DrawListHandler(update, args, $"В результаті невідомих причин, у записі про планування поста небуло закріпленного повідомлення. " +
                                                       $"Запис про планування видалено, це ніяк не зачепить чергу планування. Якщо це був збій в системі, створіть запис про планування заново.");
        }

        
        Message sentAdMessage = null;
        try
        {
            sentAdMessage = await BotClient.SendMessageToChannel(message.Message, update.GetChatId());
        }
        catch (Exception e)
        {
            message.Message.Caption = message.Message.Text = "Колись тут був текст...";
            message.Message.CaptionEntities = null;
            message.Message.Entities = null;
            await _scheduledMessagesSettings.UpdateMessage(message);
            return await DrawListHandler(update, args, $"Сталась помилка під час спроби відображення повідомлення. Текст повідомлення буде зачищений. Текст помилки:\n{e.Message}");
        }

        var publishLifetime = Math.Round(message.PublishLifetimeMinutes / 60.0, 1);
        var txt = $"{(reroutedForPath == null ? "" : reroutedForPath + "\n\n")}" +
                  $"<b>Пост для публікації у вітальному боті</b>\n" +
                  $"<b>Ідентифікатор:</b> {message.Index}\n<b>Творець:</b> {_adminUsers.GetUserName(message.Creator!.Value)}\n" +
                  $"<b>Статус:</b> {message.State}\n" +
                  $"<b>Дата створення:</b>\n    🌎 {message.CreateDate.ToDateTime():yyyy-MM-dd HH:mm}\n    🇩🇪 {message.CreateDate.ToDateTime().UtcToDeTime():yyyy-MM-dd HH:mm}\n    🇺🇦 {message.CreateDate.ToDateTime().UtcToUaTime():yyyy-MM-dd HH:mm}\n" +
                  $"<b>Дата розміщення:</b>\n    🌎 {message.PublishDate.ToDateTime():yyyy-MM-dd HH:mm}\n    🇩🇪 {message.PublishDate.ToDateTime().UtcToDeTime():yyyy-MM-dd HH:mm}\n    🇺🇦 {message.PublishDate.ToDateTime().UtcToUaTime():yyyy-MM-dd HH:mm}\n" +
                  $"<b>Час у стрічці (годин):</b> {(message.PublishLifetimeMinutes < 0 ? "без видалення" : publishLifetime)}\n" +
                  $"<b>Цільова кількість відправок:</b> {message.TargetDeliveryAmount}\n" +
                  $"<b>Дозволено для відправки?</b> {(message.AllowedToSend ? "✅" : "⛔️")}"
            ;

        if (message.State == WelcomeBotScheduledMessageState.WaitingForRemoval)
        {
            txt += $"\n<b>Чекають видалення повідомлень:</b> {message.DeliveryReport?.DeliveredMessages??0}";
        }

        var m = ComposeMessage(update)
            .SetText(txt)
            .SetButtonsInARow(2);

        if (sentAdMessage != null)
            m.RegisterMessageIdToRemoveAtPathExit(sentAdMessage.MessageId);

        if (message.State == WelcomeBotScheduledMessageState.Preparing)
        {
            m.AddButtonsForCurrentPath(new[]
            {
                ("📅 Змінити час публікації", new[] { "edate", args[1] }),
                ("⏰ Змінити час автовидалення", new[] { "elifetime", args[1] }),
                ("💬 Додати кнопку", new[] { "abutton", args[1] }),
                ("🗑️ Видалити всі кнопки", new[] { "rbutton", args[1] }),
                ("🗑 Видалити розсилку", new[] { "rpost", args[1] }),
                ("🔁 Замінити креатив", new[] { "chpost", args[1] }),
                ("🚚 Кільіксть відправок", new[] { "tcount", args[1] }),
                (message.AllowedToSend? "🔒 Заборонити відправку":"🔒 Дозволити відправку", new[] { "approve", args[1] }),
            });
        }
        else
        {
            m.AddButtonsForCurrentPath(new[]
            {
                ("🚫 Зняти з публікації", new[] { "unpublish", args[1] }),
                ("📊 Поточна статистика", new[] { "current_stats", args[1] })
            });
        }

        await m.SetNeedUpMenuButton()
            .SetNeedMainMenuButton()
            .Send();
        return CommandResult.Ok;
    }
    
    private async Task<CommandResult> DrawListHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var scheduledMessages = await _scheduledMessagesSettings.GetAllScheduledMessages();
        var txt =
            $"{(string.IsNullOrEmpty(reroutedForPath) ? "" : reroutedForPath)}В базі збережено {scheduledMessages.Length} повідомлень:";
        var m = ComposeMessage(update).SetText(txt);
        m.AddButtonsForCurrentPath(scheduledMessages
            .GroupBy(m => m.State == WelcomeBotScheduledMessageState.Preparing)
            .Reverse()
            .SelectMany(g => g.OrderBy(i => i.Index)
                .Select(m =>
                {
                    var userName = _adminUsers.GetUserName(m.Creator!.Value);
                    var statusText = "";
                    switch (m.State)
                    {
                        case WelcomeBotScheduledMessageState.Preparing:
                            break;
                        case WelcomeBotScheduledMessageState.Delivering:
                            statusText = "Доставка...";
                            break;
                        case WelcomeBotScheduledMessageState.WaitingForRemoval:
                            statusText = "Опубліковано";
                            break;
                        case WelcomeBotScheduledMessageState.Cleaning:
                            statusText = "Зачистка...";
                            break;
                        case WelcomeBotScheduledMessageState.Removed:
                            statusText = "Видалено";
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                    var label = m.State == WelcomeBotScheduledMessageState.Preparing
                        ? $"[{m.Index}] {userName}, {m.PublishDate.ToDateTime().UtcToUaTime():MM-dd HH:mm}, 👀 {m.TargetDeliveryAmount}"
                        : $"🚷 [{m.Index}, {statusText}] {userName}, {m.PublishDate.ToDateTime().UtcToUaTime():MM-dd HH:mm}, 👀 {m.TargetDeliveryAmount}";
                    return (
                        label,
                        new[] { "get", m.Index.ToString() }
                    );
                }))
            .ToArray());

        await m.SetNeedUpMenuButton()
            .SetNeedMainMenuButton()
            .SetButtonsInARow(1)
            .Send();
        return CommandResult.Ok;
    }
    
    private async Task<CommandResult> GetPublishStatusHandler(Update update, string[]? args, string? reroutedForPath)
    {
        if (!_tgUserAuthController.IsLoggedIn())
        {
            await ComposeMessage(update)
                .SetText(
                    "Нажаль, телеграм обмежує для ботів інформацію про перегляди і реакції на повідомлення. " +
                    "Для підрахунку цих даних потрібен акаунт \"живої\" людини. " +
                    "Якщо ви хочете отримати детальну інформацію по розміщунню - авторизуйте додатковий акаунт в налаштуваннях бота.")
                .SetNeedCurrentMenuButton()
                .SetNeedUpMenuButton()
                .Send();
        }
        else
        {
            var m = await _scheduledMessagesSettings.GetMessage(args[1]);
            var txt = await _scheduledMessagesPublisherHelper.GetDetailedPublicationAnalytics(m);
            await ComposeMessage(update)
                .SetText(txt)
                .SetNeedCurrentMenuButton()
                .SetNeedUpMenuButton()
                .Send();
        }
        return CommandResult.Ok;
    }
    
    private async Task<CommandResult> EditPostDateHandler(Update update, string[]? args, string? reroutedForPath)
    {   
        var txt = $"Введіть бажану дату та час в форматі <code>YYYY-MM-DD hh-mm</code>, наприклад поточний час по Києву <code>{DateTime.UtcNow.UtcToUaTime():yyyy-MM-dd HH:mm}</code>.\nВведіть час <b>по Києву</b>.";
        await _userInputAwaiting.RequestUserInput(update.GetChatId(), txt, MyPath, MyPath, new [] {"edate+", args[1]});
        return CommandResult.Ok;
    }

    private async Task<CommandResult> EditPostDateApprovedHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var userInput = update.Message.Text;
        if (DateTime.TryParseExact(userInput, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var publishTime))
        {
            publishTime = publishTime.UaToUTCTime();
            var m = await _scheduledMessagesSettings.GetMessage(args[1]);
            m.PublishDate = publishTime.ToFirestoreTimestamp();
            m.AllowedToSend = false;
            await _scheduledMessagesSettings.UpdateMessage(m);
            await DrawMessageHandler(update, args, "Дата публікації успішно оновлена!");
        }
        else
        {
            await DrawMessageHandler(update, args, "Не вдалось розпарсити дату. Перевірте формат запису і спробуйте знову.");
        }
        return CommandResult.Ok;
    }

    private async Task<CommandResult> EditTargetDeliveryCountHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var availableSubs = await _subscribersDatabase.GetAvailableToSendCount();
        var txt = $"Укажіть цільову кількість повідомлень для відправки. Якщо максимальна кількість буде більше ніж фактично можливість відправити повідомлення - повідомлення буде відправлене на максимально можливу кількість підписників. " +
                  $"На зараз кількість активних підписників становить {availableSubs}.";
        await _userInputAwaiting.RequestUserInput(update.GetChatId(), txt, MyPath, MyPath, new [] {"tcount+", args[1]});
        return CommandResult.Ok;
    }

    private async Task<CommandResult> EditTargetDeliveryCountApprovedHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var userInput = update.Message.Text;
        if (int.TryParse(userInput, out var targetAmount))
        {
            var m = await _scheduledMessagesSettings.GetMessage(args[1]);
            m.TargetDeliveryAmount = targetAmount;
            m.AllowedToSend = false;
            await _scheduledMessagesSettings.UpdateMessage(m);
            await DrawMessageHandler(update, args, "Цільова кількість відправок успішно оновлена!");
        }
        else
        {
            await DrawMessageHandler(update, args, "Не вдалось розпарсити цільову кількість відправок. Перевірте формат запису і спробуйте знову.");
        }
        return CommandResult.Ok;
    }

    
    private async Task<CommandResult> EditPostLifeTimeHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var txt = $"Через який час видалити пост?";
        await ComposeMessage(update)
            .SetText(txt)
            .AddButtonForCurrentPath("1хв", "elifetime+", args[1], "1")
            .AddButtonForCurrentPath("5хв", "elifetime+", args[1], "5")
            .AddButtonForCurrentPath("15хв", "elifetime+", args[1], "15")
            .AddButtonForCurrentPath("30хв", "elifetime+", args[1], "30")
            .AddButtonForCurrentPath("45хв", "elifetime+", args[1], "45")
            .AddButtonForCurrentPath("1 год.", "elifetime+", args[1], "60")
            .AddButtonForCurrentPath("6 год.", "elifetime+", args[1], "360")
            .AddButtonForCurrentPath("12 год.", "elifetime+", args[1], "720")
            .AddButtonForCurrentPath("18 год.", "elifetime+", args[1], "1080")
            .AddButtonForCurrentPath("24 год.", "elifetime+", args[1], "1440")
            .AddButtonForCurrentPath("47 год.", "elifetime+", args[1], "2820")
            .AddButtonForCurrentPath("Без видалення", "elifetime+", args[1], "-1")
            .SetButtonsInARow(4)
            .Send();
        return CommandResult.Ok;
    }

    private async Task<CommandResult> EditPostLifeTimeApprovedHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var userInput = args[2];
        if (int.TryParse(userInput, out var duration))
        {
            var m = await _scheduledMessagesSettings.GetMessage(args[1]);
            m.PublishLifetimeMinutes = duration;
            m.AllowedToSend = false;
            await _scheduledMessagesSettings.UpdateMessage(m);
            await DrawMessageHandler(update, args, "Час у стрічці успішно змінено!");
        }
        else
        {
            await DrawMessageHandler(update, args, "Не вдалось розпарсити час. Перевірте формат запису і спробуйте знову. Це повинне бути 1 число без зайвих символів.");
        }
        return CommandResult.Ok;
    }

    private async Task<CommandResult> AddButtonHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var txt = $"Введіть надпис на кнопці і посилання із кнопки у наступному форматі:\n<pre>Текст - посилання | Текст - посилання\nТекст - посилання | Текст - посилання</pre>\nP.S.: для переходу на новий рядок натисніть shift+enter";
        await _userInputAwaiting.RequestUserInput(update.GetChatId(), txt, MyPath, MyPath, new [] {"abutton+", args[1]});
        return CommandResult.Ok;
    }

    private async Task<CommandResult> AddButtonApprovedHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var userInput = update.Message.Text;
        var rows = userInput?.Split("\n");
        Regex urlMatch = new Regex(@"^(http(s)?://)?([\w-]+\.)+[\w-]+(/[\w- ;,./?%&=+]*)?$", RegexOptions.Singleline);

        try
        {
            var m = await _scheduledMessagesSettings.GetMessage(args[1]);
            var keyboard = m.Message.ReplyMarkup?.InlineKeyboard?.ToList() ?? new List<IEnumerable<InlineKeyboardButton>>();
            foreach (var row in rows)
            {
                var buttons = row.Split("|");
                var line = new List<InlineKeyboardButton>();
                foreach (var button in buttons)
                {
                    var parts = button.Split(" - ").Select(b=>b.Trim()).ToArray();
                    if (!urlMatch.IsMatch(parts[1])) 
                        throw new Exception($"Лінка '{parts[1]}' не відповідає регулярці '^(http(s)?://)?([\\w-]+\\.)+[\\w-]+(/[\\w- ;,./?%&=+]*)?$'");
                    line.Add(InlineKeyboardButton.WithUrl(parts[0], parts[1]));
                }
                keyboard.Add(line);
            }
            m.Message.ReplyMarkup = new InlineKeyboardMarkup(keyboard);
            m.AllowedToSend = false;
            await _scheduledMessagesSettings.UpdateMessage(m);
            await DrawMessageHandler(update, args, "Кнопка успішно додана!");
        }
        catch (Exception e)
        {
            await DrawMessageHandler(update, args, $"Не вдалось розпарсити дані кнопки. Перевірте формат запису і спробуйте знову. Текст помилки:\n<pre>{e.Message}</pre>");
        }
        return CommandResult.Ok;
    }

    private async Task<CommandResult> RemoveAllButtonsHandler(Update update, string[]? args, string? reroutedForPath)
    {   
        var txt = $"Ви впевнені що хочете видалити ВСІ кнопки у повідомлення {args[1]}?";
        await PromptUserDialog(update, txt, $"{MyPath}?rbutton+/{args[1]}",$"{MyPath}?get/{args[1]}");
        return CommandResult.Ok;
    }

    private async Task<CommandResult> RemoveAllButtonsApprovedHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var m = await _scheduledMessagesSettings.GetMessage(args[1]);
        if (m.Message.ReplyMarkup?.InlineKeyboard?.Any() ?? false)
        {
            m.Message.ReplyMarkup = null;
            m.AllowedToSend = false;
            await _scheduledMessagesSettings.UpdateMessage(m);
            await DrawMessageHandler(update, args, "Кнопки видалені!");
        }
        else
        {
            await DrawMessageHandler(update, args, "У повідомлення немає кнопок, нічого не змінилось.");
        }
        return CommandResult.Ok;
    }

    private async Task<CommandResult> RemovePostHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var txt = $"Ви впевнені що хочете видалити повідомленя {args[1]}?";
        await PromptUserDialog(update, txt, $"{MyPath}?rpost+/{args[1]}",$"{MyPath}?get/{args[1]}");
        return CommandResult.Ok;
    }

    private async Task<CommandResult> RemovePostApprovedHandler(Update update, string[]? args, string? reroutedForPath)
    {
        await _scheduledMessagesSettings.RemoveMessage(args[1]);
        await ComposeMessage(update)
            .SetText($"Повідомлення {args[1]} видалено!")
            .SetNeedCurrentMenuButton()
            .SetNeedUpMenuButton()
            .SetNeedMainMenuButton()
            .Send();
        return CommandResult.Ok;
    }

    private async Task<CommandResult> ChangePostHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var txt = $"Надішліть новий креатив для повідомлення {args[1]}";
        await _userInputAwaiting.RequestUserInput(update.GetChatId(), txt, MyPath, MyPath, new [] {"chpost+", args[1]});
        return CommandResult.Ok;
    }

    private async Task<CommandResult> ChangePostApprovedHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var mIndex = args[1];
        var message = await _scheduledMessagesSettings.GetMessage(mIndex);
        message.Message = update.Message;
        message.AllowedToSend = false;
        await _scheduledMessagesSettings.UpdateMessage(message);
        await DrawMessageHandler(update, args, "Пост оновлено!");
        return CommandResult.Ok;
    }

    private async Task<CommandResult> ApproveSendHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var mIndex = args[1];
        var m = await _scheduledMessagesSettings.GetMessage(mIndex);
        if (m.TargetDeliveryAmount == 0)
        {
            await DrawMessageHandler(update, args, "Не вказана цільова кількість відправок. Спочатку встановіть її, потім публікація стане доступна.");
            return CommandResult.Ok;
        }

        if (m.PublishLifetimeMinutes == 0)
        {
            await DrawMessageHandler(update, args, "Не встановлений час в стрічці. Встановіть або час в стрічні.");
            return CommandResult.Ok;
        }
            
        m.AllowedToSend = !m.AllowedToSend;
        await _scheduledMessagesSettings.UpdateMessage(m);
        await DrawMessageHandler(update, args, $"Статус оновлено! {(m.AllowedToSend ? "Наступне повідомлення можна відправити клієнту." : "")}");

        if (m.AllowedToSend)
        {
            var conditions = m.PublishLifetimeMinutes > 0
                ? $"{Math.Round(m.PublishLifetimeMinutes / 60.0, 1)} годин(и) розміщення."
                : "без видалення";

            var messagePreview = m?.Message?.Caption ?? m?.Message.Text ?? "повідомлення без тексту";
            messagePreview = string.Concat(messagePreview.AsSpan(0, Math.Min(30, messagePreview.Length)), "...");
            var message =
                    $"Публікація поста <pre>\"{messagePreview}\"</pre> успішно запланована для розсилки в боті на {m.PublishDate.ToDateTime().UtcToUaTime():yyyy-MM-dd HH:mm}.\n" +
                    $"Умови розміщення: {conditions}\n" +
                    $"Цільова (максимальна) кількість відправок: {m.TargetDeliveryAmount}\n" +
                    "\nЧекаємо на результати ☕️🥳"
                ;
            await _projectTeamCommunication.SendMessageToAllOwners(message);
        }

        return CommandResult.Ok;
    }
}