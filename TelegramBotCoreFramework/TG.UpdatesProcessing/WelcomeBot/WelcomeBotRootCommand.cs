using Helpers.AdminsCommunication;
using Telegram.Bot;
using TG.UpdatesProcessing.BotCommands;

namespace TG.UpdatesProcessing.WelcomeBot;

public class WelcomeBotRootCommand : BotCommandControllerBase
{
    public override string CommandName => "🤖 Вітальний бот";
    public override CommandsAccessLevel AccessLevel => CommandsAccessLevel.Owner;
    public override Type? ParentCommandType => typeof(MainMenuBotCommand);
    
    
    public WelcomeBotRootCommand(TelegramBotClient botClient, IBotCommandsFactory botCommandsFactory,
        AdminsController adminsController, AdminUsers adminUsers) : base(botClient, botCommandsFactory, adminsController, adminUsers)
    {
    }
    
    protected override Task Build()
    {
        return Task.CompletedTask;
    }
}