using System.Diagnostics;
using CommunicationChat.BotPrivateCommunication;
using Helpers;
using Helpers.AdminsCommunication;
using Helpers.Extensions;
using Helpers.PredefinedChannels;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TG.UpdatesProcessing.BotCommands;

namespace TG.UpdatesProcessing.WelcomeBot;

public class WelcomeBotSettingsCommand : BotCommandControllerBase
{
    private readonly WelcomeBotSettings _welcomeBotSettings;
    private readonly IUserInputAwaiting _userInputAwaiting;
    public override string CommandName => "⚙ Налаштування бота";
    public override CommandsAccessLevel AccessLevel => CommandsAccessLevel.Owner;
    public override Type? ParentCommandType => typeof(WelcomeBotRootCommand);
    
    
    public WelcomeBotSettingsCommand(TelegramBotClient botClient, IBotCommandsFactory botCommandsFactory,
        AdminsController adminsController, WelcomeBotSettings welcomeBotSettings,
        IUserInputAwaiting userInputAwaiting, AdminUsers adminUsers) 
        : base(botClient, botCommandsFactory, adminsController, adminUsers)
    {
        _welcomeBotSettings = welcomeBotSettings;
        _userInputAwaiting = userInputAwaiting;
    }
    
    protected override async Task Build()
    {
        await _welcomeBotSettings.LoadDefaultIfNeeded();
        
        AddDefaultShortcut(DefaultSettingsHandler);
        AddArgShortcut("_", DefaultSettingsHandler);
        AddArgShortcut("u_token", UpdateBotAccessTokenHandler);
        AddArgShortcut("u_token+", UpdateBotAccessTokenApprovedHandler);
        AddArgShortcut("comset", SetCommunicationChannelHandler);
        AddArgShortcut("comset+", SetCommunicationChannelForwardedHandler);
        AddArgShortcut("a_immediately", ApproveImmediatelyHandler);
        AddArgShortcut("a_immediately+", ApproveImmediatelyApprovedHandler);
        AddArgShortcut("a_later", AcceptLaterHandler);
        AddArgShortcut("a_later+", AcceptLaterApprovedHandler);
    }

    private async Task<CommandResult> DefaultSettingsHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var s = _welcomeBotSettings;
        var txt = $"{(string.IsNullOrEmpty(reroutedForPath) ? "" : $"{reroutedForPath}\n")}" +
            $"<b>Режим прийому заявок:</b> {(s.RequestsApproveMode == WelcomeBotSettings.BotRequestsApproveMode.Immediate ? "Авто-прийом" : "Відкладений прийом")}\n" +
            $"<b>Капча:</b> {(s.CaptchaMessage == null ? "Не задано" : "Задана")}\n" +
            $"<b>Вітальна секвенція повідомлень:</b> {s.WelcomeSequence?.Length??0} повідомлень задано\n" +
            $"<b>Канал комунікації:</b> {(s.ProxyChat == null ? "Не задано" : s.ProxyChat.GetHtmlUrl())}\n" +
            $"<b>Токен бота:</b> {(string.IsNullOrEmpty(s.BotAccessToken) ? "Не задано" : "Задано")}\n" 
            ;
        await ComposeMessage(update)
            .SetText(txt)
            .AddButtonForCurrentPath("🔑 Токен бота","u_token")
            .AddButtonForCurrentPath("📣 Чат спілкування","comset")
            .AddButtonForCurrentPath("✅ Приймати відразу","a_immediately")
            .AddButtonForCurrentPath("⏳ Приймати відкладено","a_later")
            .AddChildrenButtons()
            .SetButtonsInARow(2)
            .SetNeedUpMenuButton()
            .SetNeedMainMenuButton()
            .Send();
        return CommandResult.Ok;
    }
    
    private async Task<CommandResult> UpdateBotAccessTokenHandler(Update update, string[]? args, string? reroutedForPath)
    {
        await _userInputAwaiting.RequestUserInput(update.GetChatId(), "Введіть токен бота:", 
            MyPath, MyPath, new[] { "u_token+" });
        return CommandResult.Ok;
    }
    
    private async Task<CommandResult> UpdateBotAccessTokenApprovedHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var token = update.Message.Text;
        if (string.IsNullOrEmpty(token))
        {
            await ComposeMessage(update)
                .SetText("Токен не може бути пустим!")
                .SetNeedCurrentMenuButton()
                .Send();
            return CommandResult.Ok;
        }
        await _welcomeBotSettings.LoadDefaultIfNeeded();
        _welcomeBotSettings.BotAccessToken = token;
        await _welcomeBotSettings.SaveSettings();
        await ComposeMessage(update)
            .SetText("Токен бота успішно оновлено!")
            .SetNeedCurrentMenuButton()
            .Send();
        return CommandResult.Ok;
    }
    
    private async Task<CommandResult> SetCommunicationChannelHandler(Update update, string[]? args, string? reroutedforpath)
    {
        await _userInputAwaiting.RequestUserInput(update.GetChatId(),
            "Перешліть посилання на повідомлення в группі, формату <code>https://t.me/c/1924387865/1/4</code>. " +
            "Щоб отримати посилання - виділіть  будь-яке повідомлення в групі і натисніть \"Копіювати посилання\". " +
            "Группа повинна бути із включеними тредами, і бот в ній є адміном щоб встановити группу як группу комунікації.\n" +
            "\n ℹ В цей канал бот буде пересилати повідомлення від користувачів. Якщо ви, або будь-хто із вашої команди, відповісте на ці повідомлення в каналі - бот надішле відповідь користувачу від свого імені.", 
            MyPath, MyPath,new [] {"comset+"});
        return CommandResult.Ok;
    }

    private async Task<CommandResult> SetCommunicationChannelForwardedHandler(Update update, string[]? args,
        string? reroutedforpath)
    {
        // link example: https://t.me/c/1924387865/1/4
        var link = update.Message.Text;
        var split = link.Split('/');
        if (split.Length < 5 || !long.TryParse("-100" + split[4], out var channelId))
        {
            await ComposeMessage(update)
                .SetText(
                    "Не можу розпарсити посилання, перевірте що повідомлення було переслано корректно і повторіть знову.")
                .SetNeedCurrentMenuButton()
                .Send();
            return CommandResult.Ok;
        }

        Chat channelInfo;
        try
        {
            channelInfo = await BotClient.GetChatAsync(channelId);
            Debug.Assert(channelInfo.Type == ChatType.Group || channelInfo.Type == ChatType.Supergroup, "Чат повинен бути групою або супергрупою");
            Debug.Assert(channelInfo.IsForum ?? false, "Чат повинен бути групою з увімкнутими тредами");
        }
        catch (Exception e)
        {
            await ComposeMessage(update)
                .SetText(
                    "Не можу ідентифікувати чат. Це може статись при некоректному посилання, або якщо бот не є учасником чату. Перевірте що бот є учасником чату і що посилання на повідомлення корректне і повторіть спробу.\n" +
                    "Якщо це допоможе, то помилка: " + e.Message)
                .SetNeedCurrentMenuButton()
                .Send();
            return CommandResult.Ok;
        }
        var isMeAdmin = false;
        try
        {
            var me = await BotClient.GetMeAsync();
            var member = await BotClient.GetChatMemberAsync(channelId, me.Id);
            isMeAdmin = member.Status == ChatMemberStatus.Administrator;
        }
        catch
        {
            // do nothing
        }

        if (!isMeAdmin)
        {
            await ComposeMessage(update)
                .SetText(
                    "Бот не є адміністратором каналу. Каналом комунікації можна можна вказати тільки канал де бот є адміністратором.")
                .SetNeedCurrentMenuButton()
                .Send();
            return CommandResult.Ok;
        }

        await _welcomeBotSettings.LoadDefaultIfNeeded();
        _welcomeBotSettings.ProxyChat = new ChannelSettingsDto()
        {
            ChannelId = channelId,
            ChannelUserName = channelInfo.Username,
            ShortTitle = channelInfo.Title,
            FullTitle = channelInfo.Title
        };
        await _welcomeBotSettings.SaveSettings();

        await ComposeMessage(update)
            .SetText($"Канал {_welcomeBotSettings.ProxyChat.GetHtmlUrl()} успішно встановнелий як канал для комунікації!")
            .SetButtonsInARow(1)
            .SetNeedMainMenuButton()
            .SetNeedCurrentMenuButton()
            .Send();
        return CommandResult.Ok;
    }
    
    private async Task<CommandResult> ApproveImmediatelyHandler(Update update, string[]? args, string? reroutedForPath)
    {
        await PromptUserDialogForCurrentPath(update, "Ви впевнені що хочете змінити режим прийому заявок на приймання заявок відразу? " +
                                                     "Ця дія не впливає на поточні заявки в очікуванні, тільки на заявки що далі почнуть надходити. " +
                                                     "Щоб прийняти поточні заявки в очікуванні, виберіть відповідну команду бота.",
            "a_immediately+", "_");
        return CommandResult.Ok;
    }
    
    
    private async Task<CommandResult> ApproveImmediatelyApprovedHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var s = _welcomeBotSettings;
        s.RequestsApproveMode = WelcomeBotSettings.BotRequestsApproveMode.Immediate;
        await _welcomeBotSettings.SaveSettings();
        await DefaultSettingsHandler(update, args, "Режим прийому заявок змінено на прийом відразу");
        return CommandResult.Ok;
    }
    
    private async Task<CommandResult> AcceptLaterHandler(Update update, string[]? args, string? reroutedForPath)
    {
        await PromptUserDialogForCurrentPath(update, "Ви впевнені, що хочете змінити режим прийому заявок на відкладений прийом? Це дозволить вам вручну перевіряти заявки перед прийманням.",
            "a_later+", "_");
        return CommandResult.Ok;
    }

    private async Task<CommandResult> AcceptLaterApprovedHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var s = _welcomeBotSettings;
        s.RequestsApproveMode = WelcomeBotSettings.BotRequestsApproveMode.Deffered; // Assuming the enum for deferred mode is named "Deferred"
        await _welcomeBotSettings.SaveSettings();
        await DefaultSettingsHandler(update, args, "Режим прийому заявок змінено на відкладений прийом");
        return CommandResult.Ok;
    }
}