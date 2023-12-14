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

public class WelcomeBotCaptchaReviewCommand : BotCommandControllerBase
{
    private readonly WelcomeBotSettings _welcomeBotSettings;
    private readonly IUserInputAwaiting _userInputAwaiting;
    public override string CommandName => "👁️ Переглянути капчу";
    public override CommandsAccessLevel AccessLevel => CommandsAccessLevel.Owner;
    public override Type? ParentCommandType => typeof(WelcomeBotSettingsCommand);
    
    public WelcomeBotCaptchaReviewCommand(TelegramBotClient botClient, IBotCommandsFactory botCommandsFactory,
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
        
        AddArgShortcut("edit_captcha", EditCaptchaRequestHandler);
        AddArgShortcut("edit_captcha+", EditCaptchaReceivedHandler);
        
        AddArgShortcut("restore_captcha", RestoreDefaultCaptchaHandler);
        AddArgShortcut("restore_captcha+", RestoreDefaultCaptchaApprovedHandler);
        
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
        var sent = await BotClient.SendMessageToChannel(s.CaptchaMessage, update.GetChatId());
        var txt = $"{(string.IsNullOrEmpty(reroutedForPath) ? "" : $"{reroutedForPath}\n")}" +
                  $"Що будемо робити з капчею?";
        await ComposeMessage(update)
            .RegisterMessageIdToRemoveAtPathExit(sent.MessageId)
            .SetText(txt)
            .AddButtonsForCurrentPath(
                new[]
                {
                    ("💬 Додати кнопку", new[] { "abutton" }),
                    ("🗑️ Видалити всі кнопки", new[] { "rbutton" }),
                    ("🔁 Замінити креатив", new[] { "chpost" }),
                    ("🔄 Відновити капчу", new[] { "restore_captcha" })
                })
            .SetNeedCurrentMenuButton()
            .SetNeedUpMenuButton()
            .Send();
        return CommandResult.Ok;
    }
    
    

    private async Task<CommandResult> EditCaptchaRequestHandler(Update update, string[]? args, string? reroutedForPath)
    {
        await _userInputAwaiting.RequestUserInput(update.GetChatId(), "Введіть повідомлення для нової капчі:", MyPath, MyPath,
            new[] { "edit_captcha+" });
        return CommandResult.Ok;
    }

    private async Task<CommandResult> EditCaptchaReceivedHandler(Update update, string[]? args, string? reroutedForPath)
    {
        await _userInputAwaiting.RequestUserInput(update.GetChatId(), "Введіть нову капчу:", MyPath, MyPath, new[] { "edit_captcha+" });
        return CommandResult.Ok;
    }
    
    private async Task<CommandResult> RestoreDefaultCaptchaHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var txt = $"Ви впевнені що хочете відновити капчу до значення за замовчуванням?";
        await PromptUserDialog(update, txt, $"{MyPath}?restore_captcha+");
        return CommandResult.Ok;
    }
    
    private async Task<CommandResult> RestoreDefaultCaptchaApprovedHandler(Update update, string[]? args, string? reroutedForPath)
    {
        await _welcomeBotSettings.ResetCaptchaToDefault();
        await DefaultHandler(update, args, "Капча відновлена до значення за замовчуванням.");
        return CommandResult.Ok;
    }
    
    
    private async Task<CommandResult> AddButtonHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var txt = $"Введіть надпис на кнопці і посилання із кнопки у наступному форматі:\n<pre>Текст - посилання | Текст - посилання\nТекст - посилання | Текст - посилання</pre>\nP.S.: для переходу на новий рядок натисніть shift+enter";
        await _userInputAwaiting.RequestUserInput(update.GetChatId(), txt, MyPath, MyPath, new [] {"abutton+"});
        return CommandResult.Ok;
    }

    private async Task<CommandResult> AddButtonApprovedHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var userInput = update.Message.Text;
        var rows = userInput?.Split("\n");
        Regex urlMatch = new Regex(@"^(http(s)?://)?([\w-]+\.)+[\w-]+(/[\w- ;,./?%&=+]*)?$", RegexOptions.Singleline);

        try
        {
            var m = _welcomeBotSettings.CaptchaMessage;
            var keyboard = m.ReplyMarkup?.InlineKeyboard?.ToList() ?? new List<IEnumerable<InlineKeyboardButton>>();
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
            _welcomeBotSettings.CaptchaMessage = m;
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
        var txt = $"Ви впевнені що хочете видалити ВСІ кнопки у повідомлення?";
        await PromptUserDialog(update, txt, $"{MyPath}?rbutton+",$"{MyPath}?get");
        return CommandResult.Ok;
    }

    private async Task<CommandResult> RemoveAllButtonsApprovedHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var m = _welcomeBotSettings.CaptchaMessage;
        if (m.ReplyMarkup?.InlineKeyboard?.Any() ?? false)
        {
            m.ReplyMarkup = null;
            _welcomeBotSettings.CaptchaMessage = m;
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
        var txt = $"Надішліть новий креатив для повідомлення";
        await _userInputAwaiting.RequestUserInput(update.GetChatId(), txt, MyPath, MyPath, new [] {"chpost+"});
        return CommandResult.Ok;
    }

    private async Task<CommandResult> ChangePostApprovedHandler(Update update, string[]? args, string? reroutedForPath)
    {
        _welcomeBotSettings.CaptchaMessage = update.Message;
        await _welcomeBotSettings.SaveSettings();
        await DefaultHandler(update, args, "Пост оновлено!");
        return CommandResult.Ok;
    }
}