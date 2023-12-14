using Helpers.AdminsCommunication;
using Telegram.Bot;

namespace TG.UpdatesProcessing.BotCommands;

public class CreativesGenerationRootBotCommand : BotCommandControllerBase
{
    public override string CommandName => "⚡ Генерація креативів";
    public override CommandsAccessLevel AccessLevel => CommandsAccessLevel.Manager;
    public override Type? ParentCommandType => typeof(MainMenuBotCommand);
 
    
    public CreativesGenerationRootBotCommand(TelegramBotClient botClient, IBotCommandsFactory botCommandsFactory,
        AdminsController adminsController, AdminUsers adminUsers) : base(botClient, botCommandsFactory, adminsController, adminUsers)
    {
    }
    
    protected override Task Build() => Task.CompletedTask;
}