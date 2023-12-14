using Helpers.AdminsCommunication;
using Telegram.Bot;
using TG.UpdatesProcessing.BotCommands;
using TG.UpdatesProcessing.WelcomeBot;

namespace TG.UpdatesProcessing.WelcomeBotPostsScheduling;

public class WelcomeBotSchedulePostCommand : BotCommandControllerBase
{
    public override string CommandName => "⏳ Планування розсилок";
    public override CommandsAccessLevel AccessLevel => CommandsAccessLevel.Owner;
    public override Type? ParentCommandType => typeof(WelcomeBotRootCommand);
    
    
    public WelcomeBotSchedulePostCommand(TelegramBotClient botClient, IBotCommandsFactory botCommandsFactory,
        AdminsController adminsController, AdminUsers adminUsers) : base(botClient, botCommandsFactory, adminsController, adminUsers)
    {
    }
    
    protected override Task Build()
    {
        return Task.CompletedTask;
    }
}