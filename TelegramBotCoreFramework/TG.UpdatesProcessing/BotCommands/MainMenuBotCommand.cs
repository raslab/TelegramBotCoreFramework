using Helpers;
using Helpers.AdminsCommunication;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TG.UpdatesProcessing.BotCommands;

public class MainMenuBotCommand : BotCommandBase
{
    public static readonly string MainMenuCommandName = "🧞‍♂ Головне меню";
    
    public override string CommandName => MainMenuCommandName;
    public override CommandsAccessLevel AccessLevel => CommandsAccessLevel.Manager;
    public override Type? ParentCommandType { get; } = null;

    
    public MainMenuBotCommand(TelegramBotClient botClient, IBotCommandsFactory botCommandsFactory,
        AdminsController adminsController, AdminUsers adminUsers) 
        : base(botClient, botCommandsFactory, adminsController, adminUsers)
    {
    }


    public override async Task<CommandResult> ProcessMessage(Update update, string[]? args,
        string? reroutedForPath = null)
    {
        var text = "Ви у головному меню.";
        if (!string.IsNullOrEmpty(reroutedForPath))
        {
            text = $"Не знайдена команда {reroutedForPath}.\n" + text;
        }
        
        await ShowSubCommands(update, text);
        return CommandResult.Ok;
    }
}