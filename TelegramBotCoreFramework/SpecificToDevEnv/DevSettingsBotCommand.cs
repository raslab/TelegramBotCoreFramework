using Helpers.AdminsCommunication;
using Telegram.Bot;
using TG.UpdatesProcessing.BotCommands;

namespace SpecificToDevEnv;

public class DevSettingsBotCommand : BotCommandBase
{
    public override string CommandName => "⚙ Тестові налаштування";
    public override CommandsAccessLevel AccessLevel => CommandsAccessLevel.Owner;
    public override Type? ParentCommandType => typeof(MainMenuBotCommand);

    public DevSettingsBotCommand(TelegramBotClient botClient, IBotCommandsFactory botCommandsFactory,
        AdminsController adminsController, AdminUsers adminUsers) 
        : base(botClient, botCommandsFactory, adminsController, adminUsers)
    {
    }
}