using Google.Cloud.Firestore;
using Helpers;
using Helpers.AdminsCommunication;
using Helpers.Extensions;
using Helpers.UserAuth;
using Telegram.Bot;
using Telegram.Bot.Types;
using TG.UpdatesProcessing.BotCommands.BotSettings;

namespace TG.UpdatesProcessing.BotCommands.UserAuth;

[FirestoreData]
public class LastBotUserAuthRequesterInfo
{
    [FirestoreProperty] public long RequesterId { get; set; }
}

public class UserAuthBotCommandController : BotCommandControllerBase
{
    public override string CommandName => "🗝 Авторизація користувача";
    public override CommandsAccessLevel AccessLevel => CommandsAccessLevel.Owner;
    public override Type? ParentCommandType => typeof(BotSettingsSettingsBotCommand);

    private readonly TgUserAuthController _tgUserAuthController;
    private readonly IUserInputAwaiting _userInputAwaiting;
    private readonly ConfigurationStorage _configurationStorage;

    public UserAuthBotCommandController(TelegramBotClient botClient,
        IBotCommandsFactory botCommandsFactory,
        TgUserAuthController tgUserAuthController,
        IUserInputAwaiting userInputAwaiting, AdminsController adminsController,
        ConfigurationStorage configurationStorage, AdminUsers adminUsers) 
        : base(botClient, botCommandsFactory, adminsController, adminUsers)
    {
        _tgUserAuthController = tgUserAuthController;
        _userInputAwaiting = userInputAwaiting;
        _configurationStorage = configurationStorage;
    }

    protected override Task Build()
    {
        _tgUserAuthController.Set2FaHandler(()=>Task.Run(Handle2FaRequest));
        
        AddDefaultShortcut(DefaultCommandHandle);
        AddArgShortcut("datareq", FillUserDataHandle);
        AddArgShortcut("login", LoginHandle);
        AddArgShortcut("logout", LogoutHandle);
        
        AddArgShortcut("phone+", PhoneReceivedHandler);
        AddArgShortcut("pass+", PasswordReceivedHandler);
        AddArgShortcut("2fa+", Code2faHandler);
        return Task.CompletedTask;
    }

    private async Task Handle2FaRequest()
    {
        var requester = await _configurationStorage.Get<LastBotUserAuthRequesterInfo>();
        await _userInputAwaiting.RequestUserInputWithWeb(requester.RequesterId,
            "Телеграм повинен був відправити вам код авторизації, перешліть його мені.", MyPath, MyPath, new [] {"2fa+"});
    }

    private async Task<CommandResult> DefaultCommandHandle(Update update, string[]? args, string? reroutedForPath)
    {
        const string pre = "Тут можна опціонально додати авторизацію через користувача, для того щоб виконувати просунуті операції, котрі не можна робити через Telegram Bot API. " +
                           "Зокрема, це операції повної аналітики при видаленні постів і оновлення іменованих привітальних посилань на каналах (включаючи, посилання для заявок)." +
                           "\nКористувач повинен бути вказаний як адміністратор із відповідними правами в групах.";
        var isLoggedIn = _tgUserAuthController.IsLoggedIn();
        var txt =
            $"{(string.IsNullOrEmpty(reroutedForPath) ? pre : reroutedForPath)}" +
            $"\n\nПоточні дані по користувачу" +
            $"\n<b>Телефон:</b> {(_tgUserAuthController.UserData.ContainsKey(TgUserAuthController.PhoneNumberKey) ? _tgUserAuthController.UserData[TgUserAuthController.PhoneNumberKey] : "Не задано")}" +
            $"\n<b>Пароль:</b> {(_tgUserAuthController.UserData.ContainsKey(TgUserAuthController.PasswordKey) ? "Задано" : "Не задано")}" +
            $"\n<b>Статус авторизації:</b> {(isLoggedIn ? "Авторизовано" : "Не авторизовано")}";
        await ComposeMessage(update)
            .SetText(txt)
            .AddButtonForCurrentPath("🔐 Вказати дані для авторизації", "datareq")
            .AddButtonForCurrentPath("📲 Авторизуватись", "login")
            .AddButtonForCurrentPath("🚪 Вийти із акаунта", "logout")
            .SetButtonsInARow(1)
            .SetNeedUpMenuButton()
            .SetNeedMainMenuButton()
            .Send();
        
        return CommandResult.Ok;
    }
    
    private async Task<CommandResult> FillUserDataHandle(Update update, string[]? args, string? reroutedForPath)
    {
        var message = $"Авторизація від імені користувача потрібна для додаткових функцій бота, " +
                      $"зокрема для зчитування детальної аналітики по каналам, оновлення привітальних посилань, тощо. " +
                      $"Хоча ми і турбуємось про безпеку ваших даних і зберігаємо всі чутливі дані виключно в зашифрованому вигляді і під 7-ма замками, " +
                      $"але <b>ми категорично не радимо використовувати для авторизації основний акаунт</b>, використовуйте додатковий, не публічний акаунт." +
                      $"Окрім того, наполегливо радимо використовувати двухфакторну авторизацію на акаунті, яку можна ввімкнути в налаштуваннях аккаунта.\n\n" +
                      $"Введить номер телефону для користувача, в форматі +1234567890:";
        await _userInputAwaiting.RequestUserInput(update.GetChatId(), message, MyPath, MyPath, new [] {"phone+"});
        return CommandResult.Ok;
    }
    
    private async Task<CommandResult> LoginHandle(Update update, string[]? args, string? reroutedForPath)
    {
        try
        {
            await _configurationStorage.Push(new LastBotUserAuthRequesterInfo() { RequesterId = update.GetChatId() });
            await _tgUserAuthController.UserClient.LoginUserIfNeeded();
            return await DefaultCommandHandle(update, args, "Авторизація пройшла успішно.");
        }
        catch (Exception e)
        {
            return await DefaultCommandHandle(update, args,
                $"Помилка авторизації. Оновіть дані для авторизації і спробуйте ще раз. Якщо вам це якось допоможе, текст помилки: <code>{e.Message}</code>");
        }
    }
    
    private async Task<CommandResult> LogoutHandle(Update update, string[]? args, string? reroutedForPath)
    {
        await _tgUserAuthController.Logout();
        return await DefaultCommandHandle(update, args, "Вихід з акаунту проведений.");;
    }
    
    
    private async Task<CommandResult> PhoneReceivedHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var text = update?.Message?.Text;
        await _tgUserAuthController.SetPhone(text);
        var message = $"Введіть пароль від акануту (якщо включена подвійна аутентифікація, інакше сюди можна записати що завгодно):";
        await _userInputAwaiting.RequestUserInput(update.GetChatId(), message, MyPath, MyPath, new [] {"pass+"});
        
        return CommandResult.Ok;
    }
    
    private async Task<CommandResult> PasswordReceivedHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var text = update?.Message?.Text;
        await _tgUserAuthController.SetPassword(text);
        await ComposeMessage(update)
            .SetText("Данні збережено, можете проводити процес авторизації. Ваші повідомлення із номером телефону і паролем були видалені із чату з міркувань безпеки.")
            .SetNeedUpMenuButton()
            .SetNeedMainMenuButton()
            .Send();
        return CommandResult.Ok;
    }
    
    private async Task<CommandResult> Code2faHandler(Update update, string[]? args, string? reroutedForPath)
    {
        _tgUserAuthController.Set2Fa(update?.Message?.Text);
        return CommandResult.Ok;
    }
}