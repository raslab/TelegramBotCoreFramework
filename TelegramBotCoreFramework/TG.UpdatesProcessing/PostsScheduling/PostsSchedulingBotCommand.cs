using Helpers;
using Helpers.AdminsCommunication;
using Telegram.Bot;
using TG.UpdatesProcessing.BotCommands;

namespace TG.UpdatesProcessing.PostsScheduling;

public class PostsSchedulingBotCommand : BotCommandBase
{
    public override string CommandName => "⏳ Планування постів";
    public override CommandsAccessLevel AccessLevel => CommandsAccessLevel.Manager;
    public override Type? ParentCommandType => typeof(MainMenuBotCommand);

    public PostsSchedulingBotCommand(TelegramBotClient botClient, IBotCommandsFactory botCommandsFactory,
        AdminsController adminsController, AdminUsers adminUsers) 
        : base(botClient, botCommandsFactory, adminsController, adminUsers)
    {
    }
}