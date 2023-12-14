using Helpers;
using Helpers.AdminsCommunication;
using Helpers.Extensions;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TG.UpdatesProcessing.BotCommands;

public class ConvertToHtmlCreativeBotCommand : BotCommandControllerBase
{
    private readonly IUserInputAwaiting _userInputAwaiting;
    public override string CommandName => "🔗 Конвертувати в HTML";
    public override CommandsAccessLevel AccessLevel => CommandsAccessLevel.Owner;
    public override Type? ParentCommandType => typeof(CreativesGenerationRootBotCommand);
    
    public ConvertToHtmlCreativeBotCommand(TelegramBotClient botClient, IBotCommandsFactory botCommandsFactory,
        AdminsController adminsController, IUserInputAwaiting userInputAwaiting, AdminUsers adminUsers) 
        : base(botClient, botCommandsFactory, adminsController, adminUsers)
    {
        _userInputAwaiting = userInputAwaiting;
    }
    
    protected override Task Build()
    {
        AddDefaultShortcut(DefaultPathHandler);
        AddArgShortcut("received", ReceivedHandler);
        return Task.CompletedTask;
    }

    private async Task<CommandResult> DefaultPathHandler(Update update, string[]? args, string? reRoutedForPath)
    {
        await _userInputAwaiting.RequestUserInput(update.GetChatId(), 
            "Перешліть сюди повідомлення для парсингу HTML:",
            MyPath, MyPath, new [] {"received"});
        return CommandResult.Ok;
    }
    
    
    private async Task<CommandResult> ReceivedHandler(Update update, string[]? args, string? reRoutedForPath)
    {
        var html = update.Message.GetHTML();
        await ComposeMessage(update)
            .SetMarkdown($"HTML:\n```\n{html}\n```")
            .SetNeedUpMenuButton()
            .SetNeedMainMenuButton()
            .Send();
        return CommandResult.Ok;
    }
}