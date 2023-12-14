using System.Globalization;
using System.Text.RegularExpressions;
using Helpers;
using Helpers.AdminsCommunication;
using Helpers.Extensions;
using Helpers.PredefinedChannels;
using Helpers.UserAuth;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TG.UpdatesProcessing.BotCommands;
using TG.UpdatesProcessing.BotCommands.UserAuth;

namespace TG.UpdatesProcessing.PostsScheduling;

public class SchedulePostsListBotCommand : BotCommandControllerBase
{
    public override string CommandName => "📔 Заплановані пости";
    public override CommandsAccessLevel AccessLevel => CommandsAccessLevel.Manager;
    public override Type? ParentCommandType { get; } = typeof(PostsSchedulingBotCommand);
    
    private readonly ChannelsSettings _channelsSettings;
    private readonly IUserInputAwaiting _userInputAwaiting;
    private readonly ScheduledMessagesSettings _scheduledMessagesSettings;
    private readonly ProjectTeamCommunication _projectTeamCommunication;
    private readonly TgUserAuthController _tgUserAuthController;
    private readonly ScheduledMessagesPublisherHelper _scheduledMessagesPublisherHelper;
    private readonly AdminUsers _adminUsers;

    public SchedulePostsListBotCommand(TelegramBotClient botClient, IBotCommandsFactory botCommandsFactory,
        ChannelsSettings channelsSettings,
        IUserInputAwaiting userInputAwaiting,
        ScheduledMessagesSettings scheduledMessagesSettings,
        ProjectTeamCommunication projectTeamCommunication,
        TgUserAuthController tgUserAuthController,
        ScheduledMessagesPublisherHelper scheduledMessagesPublisherHelper, AdminsController adminsController,
        AdminUsers adminUsers) 
        : base(botClient, botCommandsFactory, adminsController, adminUsers)
    {
        _channelsSettings = channelsSettings;
        _userInputAwaiting = userInputAwaiting;
        _scheduledMessagesSettings = scheduledMessagesSettings;
        _projectTeamCommunication = projectTeamCommunication;
        _tgUserAuthController = tgUserAuthController;
        _scheduledMessagesPublisherHelper = scheduledMessagesPublisherHelper;
        _adminUsers = adminUsers;
    }

    protected override async Task Build()
    {
        await _channelsSettings.LoadSchedule();
        
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
        AddArgShortcut("achannel", AddChannelHandler);
        AddArgShortcut("achannel+", AddChannelApprovedHandler);
        AddArgShortcut("rchannel", RemoveAllChannelsHandler);
        AddArgShortcut("rchannel+", RemoveAllChannelsApprovedHandler);
        AddArgShortcut("rpost", RemovePostHandler);
        AddArgShortcut("rpost+", RemovePostApprovedHandler);
        AddArgShortcut("chpost", ChangePostHandler);
        AddArgShortcut("chpost+", ChangePostApprovedHandler);
        AddArgShortcut("approve", ApproveSendHandler);
        
        //published
        AddArgShortcut("unpublish", UnPublishHandler);
        AddArgShortcut("unpublish+", UnPublishApprovedHandler);
        AddArgShortcut("refresh_buttons", ChangeButtonsHandler);
        AddArgShortcut("refresh_buttons+", ChangeButtonsApprovedHandler);
        AddArgShortcut("refresh_buttons++", ChangeButtonsButtonsReceiveHandler);
        AddArgShortcut("refresh_post", RepublishPostRequestdHandler);
        AddArgShortcut("refresh_post1", RepublishPostApprovedHandler);
        AddArgShortcut("refresh_post2", RepublishPostButtonsHandler);
        AddArgShortcut("refresh_post3", RepublishPostButtonsReceiveHandler);
        AddArgShortcut("current_stats", GetPublishStatusHandler);
        AddArgShortcut("addlifetime", EditPublishedPostLifeTimeHandler);
        AddArgShortcut("addlifetime+", EditPublishedPostLifeTimeApprovedHandler);
    }
    
    
    private async Task<CommandResult> RepublishPostRequestdHandler(Update update, string[]? args, string? reroutedForPath)
    {   
        var txt = $"Ви впевнені що хочете <b>змінити креатив на опублікованому пості #{args[1]}</b>?";
        await PromptUserDialog(update, txt, $"{MyPath}?refresh_post1/{args[1]}",$"{MyPath}?get/{args[1]}");
        return CommandResult.Ok;
    }
    
    private async Task<CommandResult> RepublishPostApprovedHandler(Update update, string[]? args, string? reroutedForPath)
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
            var txt = $"Перешліть мені новий креатив" +
                      $"\n\nУвага! Креатив на опублікованому пості буде замінено! Якщо не впевнені - напишіть \"ні\" щоб прервати процес.";
            await _userInputAwaiting.RequestUserInput(update.GetChatId(), txt, MyPath, MyPath, new [] {"refresh_post2", args[1]});
        }
        return CommandResult.Ok;
    }
    
    
    private async Task<CommandResult> RepublishPostButtonsHandler(Update update, string[]? args, string? reroutedForPath)
    {
        try
        {
            var m = await _scheduledMessagesSettings.GetMessage(args[1]);
            m.Message = update.Message;
            await _scheduledMessagesSettings.UpdateMessage(m);
            
            var txt = $"Пост збережено!\nДайте опис нових кнопок:\n<pre>Текст - посилання | Текст - посилання\nТекст - посилання | Текст - посилання</pre>" +
                      $"\n\nЯкщо кнопки не треба - просто напишіть 'ні' або що завгодно не по формату тексту.";
            await _userInputAwaiting.RequestUserInput(update.GetChatId(), txt, MyPath, MyPath, new [] {"refresh_post3", args[1]});
        }
        catch (Exception e)
        {
            await DrawMessageHandler(update, args, $"Не вдалось розпарсити дані кнопки. Перевірте формат запису і спробуйте знову. Текст помилки:\n<pre>{e.Message}</pre>");
        }
        return CommandResult.Ok;
    }
    
    private async Task<CommandResult> RepublishPostButtonsReceiveHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var userInput = update.Message.Text;
        var rows = userInput?.Split("\n");
        Regex urlMatch = new Regex(@"^(http(s)?://)?([\w-]+\.)+[\w-]+(/[\w- ;,./?%&=+]*)?$", RegexOptions.Singleline);

        try
        {
            var m = await _scheduledMessagesSettings.GetMessage(args[1]);
            var keyboard = new List<IEnumerable<InlineKeyboardButton>>();
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
            await _scheduledMessagesPublisherHelper.UpdatePublishedPosts(m);
            await _scheduledMessagesSettings.UpdateMessage(m);
            await DrawMessageHandler(update, args, "Пост оновлено!");
        }
        catch (Exception e)
        {
            await DrawMessageHandler(update, args, $"Не вдалось розпарсити дані кнопки. Перевірте формат запису і спробуйте знову. Текст помилки:\n<pre>{e.Message}</pre>");
        }
        return CommandResult.Ok;
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
            await _scheduledMessagesPublisherHelper.TerminateFromPublication(message,
                $"Користувач {me.DisplayName} (id {me.UserId}) вручну зняв із публікації пост {message.Index}");
        }
        return CommandResult.Ok;
    }
    
    private async Task<CommandResult> ChangeButtonsHandler(Update update, string[]? args, string? reroutedForPath)
    {   
        var txt = $"Ви впевнені що хочете <b>змінити кнопки на опублікованому пості #{args[1]}</b>?";
        await PromptUserDialog(update, txt, $"{MyPath}?refresh_buttons+/{args[1]}",$"{MyPath}?get/{args[1]}");
        return CommandResult.Ok;
    }
    
    private async Task<CommandResult> ChangeButtonsApprovedHandler(Update update, string[]? args, string? reroutedForPath)
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
            var txt = $"Дайте опис нових кнопок:\n<pre>Текст - посилання | Текст - посилання\nТекст - посилання | Текст - посилання</pre>" +
                      $"\n\nУвага! Всі кнопки на всіх опублікованих постах будуть ЗАМІНЕНІ на те що ви зараз відправите. Якщо не впевнені - напишіть \"ні\" щоб прервати процес.";
            await _userInputAwaiting.RequestUserInput(update.GetChatId(), txt, MyPath, MyPath, new [] {"refresh_buttons++", args[1]});
        }
        return CommandResult.Ok;
    }
    
    private async Task<CommandResult> ChangeButtonsButtonsReceiveHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var userInput = update.Message.Text;
        var rows = userInput?.Split("\n");
        Regex urlMatch = new Regex(@"^(http(s)?://)?([\w-]+\.)+[\w-]+(/[\w- ;,./?%&=+]*)?$", RegexOptions.Singleline);

        try
        {
            var m = await _scheduledMessagesSettings.GetMessage(args[1]);
            var keyboard = new List<IEnumerable<InlineKeyboardButton>>();
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
            await _scheduledMessagesPublisherHelper.UpdateButtonsOnPublishedPosts(m);
            await _scheduledMessagesSettings.UpdateMessage(m);
            await DrawMessageHandler(update, args, "Кнопки успішно замінені!");
        }
        catch (Exception e)
        {
            await DrawMessageHandler(update, args, $"Не вдалось розпарсити дані кнопки. Перевірте формат запису і спробуйте знову. Текст помилки:\n<pre>{e.Message}</pre>");
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
                  $"<b>Пост для публікації у каналах</b>\n" +
                  $"<b>Ідентифікатор:</b> {message.Index}\n<b>Творець:</b> {_adminUsers.GetUserName(message.Creator!.Value)}\n" +
                  $"<b>Статус:</b> {message.State}\n" +
                  $"<b>Дата створення:</b>\n    🌎 {message.CreateDate.ToDateTime():yyyy-MM-dd HH:mm}\n    🇩🇪 {message.CreateDate.ToDateTime().UtcToDeTime():yyyy-MM-dd HH:mm}\n    🇺🇦 {message.CreateDate.ToDateTime().UtcToUaTime():yyyy-MM-dd HH:mm}\n" +
                  $"<b>Дата розміщення:</b>\n    🌎 {message.PublishDate.ToDateTime():yyyy-MM-dd HH:mm}\n    🇩🇪 {message.PublishDate.ToDateTime().UtcToDeTime():yyyy-MM-dd HH:mm}\n    🇺🇦 {message.PublishDate.ToDateTime().UtcToUaTime():yyyy-MM-dd HH:mm}\n" +
                  $"<b>Час у стрічці (годин):</b> {(message.PublishLifetimeMinutes < 0 ? "без видалення" : publishLifetime)}\n" +
                  $"<b>В канали:</b> [{(!(message.ChatIdsToSend?.Any() ?? false) ? "не задано" : string.Join(", ", message.ChatIdsToSend.Select(targetChat => _channelsSettings.ChannelSettings.FirstOrDefault(i => i.ChannelId == targetChat).GetHtmlUrl(true))))}]\n" +
                  $"<b>Дозволено для відправки?</b> {(message.AllowedToSend ? "✅" : "⛔️")}"
            ;

        if (message.State == ScheduledMessageState.WaitingForRemoval)
        {
            var infos = message.SentMessages
                .Select(c=>new 
                {
                    c.ChatId, 
                    messageId = c.MessageId,
                    scheduleInfo = _channelsSettings.ChannelSettings.First(e=>e.ChannelId == c.ChatId)
                }).ToArray();
            var urls = string.Join(",\n ",infos.Select(info=>_channelsSettings.ChannelSettings.FirstOrDefault(i=>i.ChannelId == info.ChatId).GetHtmlUrl(false, info.messageId)));
            txt += $"\n<b>Посилання:</b>\n {urls}";
        }

        var m = ComposeMessage(update)
            .SetText(txt)
            .SetButtonsInARow(2);

        if (sentAdMessage != null)
            m.RegisterMessageIdToRemoveAtPathExit(sentAdMessage.MessageId);

        if (message.State == ScheduledMessageState.Preparing)
        {
            m.AddButtonsForCurrentPath(new[]
            {
                ("📅 Змінити час публікації", new[] { "edate", args[1] }),
                ("⏰ Змінити час у стрічці", new[] { "elifetime", args[1] }),
                ("💬 Додати кнопку", new[] { "abutton", args[1] }),
                ("🗑️ Видалити всі кнопки", new[] { "rbutton", args[1] }),
                ("➕ Додати канали", new[] { "achannel", args[1] }),
                ("➖ Видалити всі канали", new[] { "rchannel", args[1] }),
                ("🗑 Видалити пост", new[] { "rpost", args[1] }),
                ("🔁 Замінити креатив", new[] { "chpost", args[1] }),
                ("🔒 Дозволити/заборонити відправку", new[] { "approve", args[1] }),
            });
        }
        else
        {
            m.AddButtonsForCurrentPath(new[]
            {
                ("⏰ Змінити час у стрічці", new[] { "addlifetime", args[1] }),
                ("🚫 Зняти з публікації", new[] { "unpublish", args[1] }),
                ("🔁 Замінити креатив", new[] { "refresh_post", args[1] }),
                ("🔄 Оновити кнопки", new[] { "refresh_buttons", args[1] }),
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
            .GroupBy(m => m.State == ScheduledMessageState.Preparing)
            .Reverse()
            .SelectMany(g => g.OrderBy(i => i.Index)
                .Select(m =>
                {
                    var userName = _adminUsers.GetUserName(m.Creator!.Value);
                    var chatsList = "---";
                    if (m.ChatIdsToSend?.Any() ?? false)
                    {
                        var names = m.ChatIdsToSend
                            .Select(targetChat =>
                                _channelsSettings.ChannelSettings.FirstOrDefault(i => i.ChannelId == targetChat))
                            .Select(i => i.ShortTitle);
                        chatsList = string.Join("+", names);
                    }

                    var label = m.State == ScheduledMessageState.Preparing
                        ? $"[{m.Index}] {userName}, {m.PublishDate.ToDateTime().UtcToUaTime():MM-dd HH:mm}, {chatsList}"
                        : $"🚷 [{m.Index}, Опубліковано] {userName}, {m.PublishDate.ToDateTime().UtcToUaTime():MM-dd HH:mm}, {chatsList}";
                    return (
                        label,
                        new[] { "get", m.Index.ToString() }
                    );
                }))
            .ToArray());

        await m.SetNeedUpMenuButton()
            .SetButtonsInARow(1)
            .SetNeedMainMenuButton()
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

    private async Task<CommandResult> EditPostLifeTimeHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var txt = $"Через який час видалити пост?";
        await ComposeMessage(update)
            .SetText(txt)
            .AddButtonForCurrentPath("5хв", "elifetime+", args[1], "5")
            .AddButtonForCurrentPath("15хв", "elifetime+", args[1], "15")
            .AddButtonForCurrentPath("30хв", "elifetime+", args[1], "30")
            .AddButtonForCurrentPath("45хв", "elifetime+", args[1], "45")
            .AddButtonForCurrentPath("1 год.", "elifetime+", args[1], "60")
            .AddButtonForCurrentPath("6 год.", "elifetime+", args[1], "360")
            .AddButtonForCurrentPath("12 год.", "elifetime+", args[1], "720")
            .AddButtonForCurrentPath("18 год.", "elifetime+", args[1], "1080")
            .AddButtonForCurrentPath("24 год.", "elifetime+", args[1], "1440")
            .AddButtonForCurrentPath("48 год.", "elifetime+", args[1], "2880")
            .AddButtonForCurrentPath("72 год.", "elifetime+", args[1], "4320")
            .AddButtonForCurrentPath("96 год.", "elifetime+", args[1], "5760")
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
    
    
    private async Task<CommandResult> EditPublishedPostLifeTimeHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var txt = $"Через який час видалити пост?";
        await ComposeMessage(update)
            .SetText(txt)
            .AddButtonForCurrentPath("-5хв", "addlifetime+", args[1], "-5")
            .AddButtonForCurrentPath("-15хв", "addlifetime+", args[1], "-15")
            .AddButtonForCurrentPath("-30хв", "addlifetime+", args[1], "-30")
            .AddButtonForCurrentPath("-45хв", "addlifetime+", args[1], "-45")
            .AddButtonForCurrentPath("-1 год.", "addlifetime+", args[1], "-60")
            .AddButtonForCurrentPath("-2 год.", "addlifetime+", args[1], "-120")
            .AddButtonForCurrentPath("-3 год.", "addlifetime+", args[1], "-180")
            .AddButtonForCurrentPath("-5 год.", "addlifetime+", args[1], "-300")
            .AddButtonForCurrentPath("+5хв", "addlifetime+", args[1], "5")
            .AddButtonForCurrentPath("+15хв", "addlifetime+", args[1], "15")
            .AddButtonForCurrentPath("+30хв", "addlifetime+", args[1], "30")
            .AddButtonForCurrentPath("+45хв", "addlifetime+", args[1], "45")
            .AddButtonForCurrentPath("+1 год.", "addlifetime+", args[1], "60")
            .AddButtonForCurrentPath("+2 год.", "addlifetime+", args[1], "120")
            .AddButtonForCurrentPath("+3 год.", "addlifetime+", args[1], "180")
            .AddButtonForCurrentPath("+5 год.", "addlifetime+", args[1], "300")
            .SetButtonsInARow(4)
            .Send();
        return CommandResult.Ok;
    }

    private async Task<CommandResult> EditPublishedPostLifeTimeApprovedHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var userInput = args[2];
        if (int.TryParse(userInput, out var duration))
        {
            var m = await _scheduledMessagesSettings.GetMessage(args[1]);
            m.PublishLifetimeMinutes += duration;
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

    private async Task<CommandResult> AddChannelHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var m = await _scheduledMessagesSettings.GetMessage(args[1]);
        var existsChats = m.ChatIdsToSend ?? new List<long>();
        await ComposeMessage(update)
            .SetText("В який чат додати?")
            .AddButtonsForCurrentPath(
                _channelsSettings.ChannelSettings
                    .Select((e, index) => new { e.ChannelId, SmallDescription = e.ShortTitle, index })
                    .Where(e => !existsChats.Contains(e.ChannelId))
                    .Select(e => (e.SmallDescription, new[] { "achannel+", args[1], e.index.ToString() }))
                    .ToArray()
            )
            .AddButtonForCurrentPath("Всі канали","achannel+", args[1], "a")
            .SetButtonsInARow(3)
            .Send();
        return CommandResult.Ok;
    }

    private async Task<CommandResult> AddChannelApprovedHandler(Update update, string[]? args,
        string? reroutedForPath)
    {
        var isLong = long.TryParse(args[2], out var index);
        if (args[2] == "a" || isLong)
        {
            var m = await _scheduledMessagesSettings.GetMessage(args[1]);
            if (m.ChatIdsToSend == null) m.ChatIdsToSend = new List<long>();
            if (args[2] == "a")
            {
                var channelsToAdd = _channelsSettings.ChannelSettings
                    .Where(e => !m.ChatIdsToSend.Contains(e.ChannelId))
                    .Select(e => e.ChannelId);
                m.ChatIdsToSend.AddRange(channelsToAdd);
            }
            else
            {
                m.ChatIdsToSend.Add(_channelsSettings.ChannelSettings[index].ChannelId);
            }
            m.ChatIdsToSend = m.ChatIdsToSend.Distinct().ToList();
            m.AllowedToSend = false;
            await _scheduledMessagesSettings.UpdateMessage(m);
            await DrawMessageHandler(update, args, "Канали додані!");
        }
        else
        {
            await DrawMessageHandler(update, args, "Помилка зчитування аргумента цільового каналу.");
        }

        return CommandResult.Ok;
    }

    private async Task<CommandResult> RemoveAllChannelsHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var txt = $"Ви впевнені що хочете видалити ВСІ цільові канали для публікації {args[1]}?";
        await PromptUserDialog(update, txt, $"{MyPath}?rchannel+/{args[1]}",$"{MyPath}?get/{args[1]}");
        return CommandResult.Ok;
    }

    private async Task<CommandResult> RemoveAllChannelsApprovedHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var m = await _scheduledMessagesSettings.GetMessage(args[1]);
        if (m.ChatIdsToSend?.Any() ?? false)
        {
            m.ChatIdsToSend = new List<long>();
            m.AllowedToSend = false;
            await _scheduledMessagesSettings.UpdateMessage(m);
            await DrawMessageHandler(update, args, "Канали видалені!");
        }
        else
        {
            await DrawMessageHandler(update, args, "Канали не були назначені, нічого не змінилось.");
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
        if (!(m.ChatIdsToSend?.Any() ?? false))
        {
            await DrawMessageHandler(update, args, "Не встановлені канали куди публікувати. Спочатку встановіть канали, потім публікація стане доступна.");
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
            await _channelsSettings.LoadSchedule();
            var infos = _channelsSettings.ChannelSettings
                .Where(e => m.ChatIdsToSend.Contains(e.ChannelId))
                .Select(c => new
                {
                    c.ChannelId,
                    scheduleInfo = c
                });

            var conditions = m.PublishLifetimeMinutes > 0
                ? $"{Math.Round(m.PublishLifetimeMinutes / 60.0, 1)} годин(и) в стрічці."
                : "без видалення";

            var messagePreview = m?.Message?.Caption ?? m?.Message.Text ?? "повідомлення без тексту";
            messagePreview = string.Concat(messagePreview.AsSpan(0, Math.Min(30, messagePreview.Length)), "...");
            var message =
                    $"Публікація поста <pre>\"{messagePreview}\"</pre> успішно запланована на {m.PublishDate.ToDateTime().UtcToUaTime():yyyy-MM-dd HH:mm}.\n" +
                    $"Умови розміщення: {conditions}\n" +
                    $"В канали:\n {string.Join(",\n ", infos.Select(i => $"{_channelsSettings.ChannelSettings.First(c => c.ChannelId == i.ChannelId).GetHtmlUrl()}"))}\n" +
                    "\nЧекаємо на результати ☕️🥳"
                ;
            await _projectTeamCommunication.SendMessageToAllOwners(message);
        }

        return CommandResult.Ok;
    }
}