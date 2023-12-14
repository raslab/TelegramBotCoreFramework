using Helpers;
using Helpers.AdminsCommunication;
using Helpers.Extensions;
using Telegram.Bot;
using Telegram.Bot.Types;
using TG.UpdatesProcessing.BotCommands;

namespace TG.UpdatesProcessing.PostsScheduling;

public class ScheduleNewPostBotCommand : BotCommandControllerBase
{
    private readonly IUserInputAwaiting _userInputAwaiting;
    private readonly ScheduledMessagesSettings _scheduledMessagesSettings;
    public override string CommandName => "✍ Створити пост";
    public override CommandsAccessLevel AccessLevel => CommandsAccessLevel.Manager;
    public override Type? ParentCommandType => typeof(PostsSchedulingBotCommand);
    
    public ScheduleNewPostBotCommand(TelegramBotClient botClient,
        IBotCommandsFactory botCommandsFactory,
        IUserInputAwaiting userInputAwaiting,
        ScheduledMessagesSettings scheduledMessagesSettings, AdminsController adminsController, AdminUsers adminUsers) 
        : base(botClient, botCommandsFactory, adminsController, adminUsers)
    {
        _userInputAwaiting = userInputAwaiting;
        _scheduledMessagesSettings = scheduledMessagesSettings;
    }
    
    protected override Task Build()
    {
        AddDefaultShortcut(DefaultCommandHandle);
        AddArgShortcut("add+", MessageReceivedHandle);
        return Task.CompletedTask;
    }

    private async Task<CommandResult> DefaultCommandHandle(Update update, string[]? args, string? reroutedForPath)
    {
        await _userInputAwaiting.RequestUserInput(update.GetChatId(), 
            "Введіть повідомлення яке ви хочете запланувати (ви можете переслати мені повідомлення, не обов'язково його вводити вручну):",
            MyPath, MyPath,new [] {"add+"});
        return CommandResult.Ok;
    }
    
    
    private async Task<CommandResult> MessageReceivedHandle(Update update, string[]? args, string? reroutedForPath)
    {
        var m = await _scheduledMessagesSettings.AddMessage(update.GetChatId(), update.Message);

        var message = "Повідомлення додано!";

        await ComposeMessage(update)
            .SetText(message)
            .AddButtonForPath<SchedulePostsListBotCommand>("✏ Редагувати пост", "get", m.Index.ToString())
            .SetNeedUpMenuButton()
            .SetNeedUpMenuButton()
            .Send();
        return CommandResult.Ok;
    }
}