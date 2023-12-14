using Helpers;
using Helpers.AdminsCommunication;
using Telegram.Bot;

namespace TG.UpdatesProcessing.BotCommands.BotSettings;


public class BotSettingsSettingsBotCommand : BotCommandBase
{
    public override string CommandName => "⚙ Налаштування бота";
    public override CommandsAccessLevel AccessLevel => CommandsAccessLevel.Owner;
    public override Type? ParentCommandType => typeof(MainMenuBotCommand);

    public BotSettingsSettingsBotCommand(TelegramBotClient botClient, IBotCommandsFactory botCommandsFactory,
        AdminsController adminsController, AdminUsers adminUsers) 
        : base(botClient, botCommandsFactory, adminsController, adminUsers)
    {
    }
}