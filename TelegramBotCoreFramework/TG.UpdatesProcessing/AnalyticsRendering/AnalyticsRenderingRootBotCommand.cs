using Helpers.AdminsCommunication;
using Telegram.Bot;

namespace TG.UpdatesProcessing.BotCommands;

public class AnalyticsRenderingRootBotCommand : BotCommandControllerBase
{
    public override string CommandName => "📊 Аналітика";
    public override CommandsAccessLevel AccessLevel => CommandsAccessLevel.Owner;
    public override Type? ParentCommandType => typeof(MainMenuBotCommand);
    
    public AnalyticsRenderingRootBotCommand(TelegramBotClient botClient, IBotCommandsFactory botCommandsFactory,
        AdminsController adminsController, AdminUsers adminUsers) 
        : base(botClient, botCommandsFactory, adminsController, adminUsers)
    {
    }
    
    
    protected override Task Build()
    {
        return Task.CompletedTask;
    }
}