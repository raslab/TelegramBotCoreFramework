using Helpers.AdminsCommunication;
using Telegram.Bot;
using Telegram.Bot.Types;
using TG.UpdatesProcessing.BotCommands;

namespace SpecificToDevEnv;

public class PrintHelloWorldBotCommand : BotCommandBase
{
    public override string CommandName => "👋 Привіт світ";
    public override CommandsAccessLevel AccessLevel => CommandsAccessLevel.Owner;
    public override Type? ParentCommandType => typeof(DevSettingsBotCommand);

    public PrintHelloWorldBotCommand(TelegramBotClient botClient, IBotCommandsFactory botCommandsFactory,
        AdminsController adminsController, AdminUsers adminUsers) 
        : base(botClient, botCommandsFactory, adminsController, adminUsers)
    {
    }

    public override async Task<CommandResult> ProcessMessage(Update update, string[]? args,
        string? reroutedForPath = null)
    {
        await SendTextMessageWithDefaultCommandButton(update, "Hello world!");
        return CommandResult.Ok;
    }
}