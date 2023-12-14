using System.Text.RegularExpressions;
using CommunicationChat.BotPrivateCommunication;
using Helpers;
using Helpers.AdminsCommunication;
using Helpers.Extensions;
using Helpers.PredefinedChannels;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TG.UpdatesProcessing.BotCommands;

namespace TG.UpdatesProcessing.WelcomeBot;

public class WelcomeBotWelcomeSequenceReviewCommand : BotCommandControllerBase
{
    private readonly WelcomeBotSettings _welcomeBotSettings;
    private readonly IUserInputAwaiting _userInputAwaiting;
    public override string CommandName => "👁️ Вітальна секвенція";
    public override CommandsAccessLevel AccessLevel => CommandsAccessLevel.Owner;
    public override Type? ParentCommandType => typeof(WelcomeBotSettingsCommand);

    public WelcomeBotWelcomeSequenceReviewCommand(TelegramBotClient botClient, IBotCommandsFactory botCommandsFactory,
        AdminsController adminsController, WelcomeBotSettings welcomeBotSettings,
        AdminUsers adminUsers, IUserInputAwaiting userInputAwaiting)
        : base(botClient, botCommandsFactory, adminsController, adminUsers)
    {
        _welcomeBotSettings = welcomeBotSettings;
        _userInputAwaiting = userInputAwaiting;
    }

    protected override async Task Build()
    {
        await _welcomeBotSettings.LoadDefaultIfNeeded();
        
        AddDefaultShortcut(DefaultHandler);
        
        // general
        AddArgShortcut("add_message", AddMessageRequestHandler);
        AddArgShortcut("add_message+", AddMessageReceivedHandler);
        AddArgShortcut("clean", CleanWelcomeSequenceHandler);
        AddArgShortcut("clean+", CleanWelcomeSequenceApprovedHandler);
        
        AddArgShortcut("restore_seq", RestoreDefaultWelcomeSequenceHandler);
        AddArgShortcut("restore_seq+", RestoreDefaultWelcomeSequenceApprovedHandler);
        
        // for each message
        AddArgShortcut("editm", EditMessageActionsHandler);
        AddArgShortcut("abutton", AddButtonHandler);
        AddArgShortcut("abutton+", AddButtonApprovedHandler);
        AddArgShortcut("rbutton", RemoveAllButtonsHandler);
        AddArgShortcut("rbutton+", RemoveAllButtonsApprovedHandler);
        AddArgShortcut("chpost", ChangePostHandler);
        AddArgShortcut("chpost+", ChangePostApprovedHandler);   
    }
    
    private async Task<CommandResult> DefaultHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var s = _welcomeBotSettings;
        var seqMessages = s.WelcomeSequence?.ToArray() ?? Array.Empty<Message>();
        
        var txt = $"{(string.IsNullOrEmpty(reroutedForPath) ? "" : $"{reroutedForPath}\n")}" +
                  $"Вітальна сиквенція {(seqMessages.Any()?"(\u2b06\ufe0f)":"")} містить <b>{seqMessages.Length}</b> повідомлень.\n" +
                  $"Що будемо робити з вітальною секвенцією?";
        var m = ComposeMessage(update)
            .SetText(txt)
            .AddButtonsForCurrentPath(
                new[]
                {
                    ("💬 Додати повідомлення", new[] { "add_message" }),
                })
            .SetNeedCurrentMenuButton()
            .SetNeedUpMenuButton();

        if (seqMessages.Any())
        {
            for (var index = 0; index < seqMessages.Length; index++)
            {
                var message = seqMessages[index];
                var sent = await BotClient.SendMessageToChannel(message, update.GetChatId());
                m.RegisterMessageIdToRemoveAtPathExit(sent.MessageId)
                    .AddButtonForCurrentPath($"📝 Редагувати повідомлення {index + 1}", new[] { "editm", $"{index}" });
            }
        }
        
        await m
            .AddButtonsForCurrentPath(new []
            {
                ("🔄 Відновити секвенцію", new[] { "restore_seq" }),
                ("🗑️ Очистити секвенцію", new[] { "clean" })
            })
            .SetButtonsInARow(1)
            .Send();
        return CommandResult.Ok;
    }
    
    private async Task<CommandResult> AddMessageRequestHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var txt = $"Надішліть повідомлення для вітальної секвенції";
        await _userInputAwaiting.RequestUserInput(update.GetChatId(), txt, MyPath, MyPath, new [] {"add_message+"});
        return CommandResult.Ok;
    }
    
    private async Task<CommandResult> AddMessageReceivedHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var s = _welcomeBotSettings;
        var seqMessages = s.WelcomeSequence?.ToList() ?? new List<Message>();
        seqMessages.Add(update.Message);
        s.WelcomeSequence = seqMessages.ToArray();
        await s.SaveSettings();
        await DefaultHandler(update, args, "Повідомлення додано!");
        return CommandResult.Ok;
    }
    
    private async Task<CommandResult> RestoreDefaultWelcomeSequenceHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var txt = $"Ви впевнені що хочете відновити вітальну секвенцію до значення за замовчуванням?";
        await PromptUserDialog(update, txt, $"{MyPath}?restore_seq+", $"{MyPath}");
        return CommandResult.Ok;
    }
    
    private async Task<CommandResult> RestoreDefaultWelcomeSequenceApprovedHandler(Update update, string[]? args, string? reroutedForPath)
    {
        await _welcomeBotSettings.ResetWelcomeSequenceToDefault();
        await DefaultHandler(update, args, "Вітальна секвенція відновлена до значення за замовчуванням.");
        return CommandResult.Ok;
    }
    
    private async Task<CommandResult> EditMessageActionsHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var index = int.Parse(args![1]);
        var txt = $"Що будемо робити з повідомленням {index + 1}?";
        
        var m = ComposeMessage(update)
            .SetText(txt)
            .AddButtonsForCurrentPath(
                new[]
                {
                    ("💬 Додати кнопку", new[] { "abutton", $"{index}" }),
                    ("🗑️ Видалити всі кнопки", new[] { "rbutton", $"{index}" }),
                    ("🔁 Замінити креатив", new[] { "chpost", $"{index}" })
                })
            .SetNeedCurrentMenuButton()
            .SetNeedUpMenuButton();
        
        await m
            .SetNeedCurrentMenuButton()
            .SetNeedUpMenuButton()
            .Send();
        
        return CommandResult.Ok;
    }
    
    private async Task<CommandResult> AddButtonHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var index = int.Parse(args![1]);
        var txt = $"Введіть надпис на кнопці і посилання із кнопки у наступному форматі:\n<pre>Текст - посилання | Текст - посилання\nТекст - посилання | Текст - посилання</pre>\nP.S.: для переходу на новий рядок натисніть shift+enter";
        await _userInputAwaiting.RequestUserInput(update.GetChatId(), txt, MyPath, MyPath, new [] {"abutton+", $"{index}"});
        return CommandResult.Ok;
    }
    
    private async Task<CommandResult> AddButtonApprovedHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var index = int.Parse(args![1]);
        var userInput = update.Message.Text;
        var rows = userInput?.Split("\n");
        Regex urlMatch = new Regex(@"^(http(s)?://)?([\w-]+\.)+[\w-]+(/[\w- ;,./?%&=+]*)?$", RegexOptions.Singleline);

        try
        {
            var m = _welcomeBotSettings.WelcomeSequence![index];
            var keyboard = m?.ReplyMarkup?.InlineKeyboard?.ToList() ?? new List<IEnumerable<InlineKeyboardButton>>();
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
            m.ReplyMarkup = new InlineKeyboardMarkup(keyboard);
            _welcomeBotSettings.WelcomeSequence[index] = m;
            await _welcomeBotSettings.SaveSettings();
            await DefaultHandler(update, args, "Кнопка успішно додана!");
        }
        catch (Exception e)
        {
            await DefaultHandler(update, args, $"Не вдалось розпарсити дані кнопки. Перевірте формат запису і спробуйте знову. Текст помилки:\n<pre>{e.Message}</pre>");
        }
        return CommandResult.Ok;
    }
    
    private async Task<CommandResult> RemoveAllButtonsHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var index = int.Parse(args![1]);
        var txt = $"Ви впевнені що хочете видалити ВСІ кнопки у повідомлення {index + 1}?";
        await PromptUserDialog(update, txt, $"{MyPath}?rbutton+/{index}", $"{MyPath}");
        return CommandResult.Ok;
    }
    
    private async Task<CommandResult> RemoveAllButtonsApprovedHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var index = int.Parse(args![1]);
        var m = _welcomeBotSettings.WelcomeSequence?[index];
        if (m?.ReplyMarkup?.InlineKeyboard?.Any() ?? false)
        {
            _welcomeBotSettings.WelcomeSequence[index].ReplyMarkup = null;
            await _welcomeBotSettings.SaveSettings();
            await DefaultHandler(update, args, "Кнопки видалені!");
        }
        else
        {
            await DefaultHandler(update, args, "У повідомлення немає кнопок, нічого не змінилось.");
        }
        return CommandResult.Ok;
    }
    
    private async Task<CommandResult> ChangePostHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var index = int.Parse(args![1]);
        var txt = $"Надішліть новий креатив для повідомлення {index + 1}";
        await _userInputAwaiting.RequestUserInput(update.GetChatId(), txt, MyPath, MyPath, new [] {"chpost+", $"{index}"});
        return CommandResult.Ok;
    }
    
    private async Task<CommandResult> ChangePostApprovedHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var index = int.Parse(args![1]);
        _welcomeBotSettings.WelcomeSequence[index] = update.Message;
        await _welcomeBotSettings.SaveSettings();
        await DefaultHandler(update, args, "Пост оновлено!");
        return CommandResult.Ok;
    }
    
    private async Task<CommandResult> CleanWelcomeSequenceHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var txt = $"Ви впевнені що хочете очистити вітальну секвенцію?";
        await PromptUserDialog(update, txt, $"{MyPath}?clean+", $"{MyPath}");
        return CommandResult.Ok;
    }
    
    private async Task<CommandResult> CleanWelcomeSequenceApprovedHandler(Update update, string[]? args, string? reroutedForPath)
    {
        _welcomeBotSettings.WelcomeSequence = Array.Empty<Message>();
        await _welcomeBotSettings.SaveSettings();
        await DefaultHandler(update, args, "Вітальна секвенція очищена!");
        return CommandResult.Ok;
    }
}