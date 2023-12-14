using Helpers;
using Helpers.AdminsCommunication;
using Helpers.Extensions;
using Helpers.PredefinedChannels;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TG.UpdatesProcessing.BotCommands;

public interface IBotCommand
{
    public string CommandName {get;}
    public CommandsAccessLevel AccessLevel { get; }
    public Type? ParentCommandType { get; }

    public void SetCommandPath(string path);
    public Task<CommandResult> ProcessMessage(Update update, string[]? args, string? reroutedForPath = null);
}

public enum CommandResult
{
    Ok,
    ShowMainMenu
}

public abstract class BotCommandBase: IBotCommand
{
    private readonly IBotCommandsFactory _botCommandsFactory;
    private readonly AdminsController _adminsController;
    private readonly AdminUsers _adminUsers;
    protected TelegramBotClient BotClient {get; private set;}
    protected string MyPath { get; private set; }

    public abstract string CommandName {get;}
    public abstract CommandsAccessLevel AccessLevel {get;}
    public abstract Type? ParentCommandType { get; }

    public BotCommandBase(TelegramBotClient botClient, IBotCommandsFactory botCommandsFactory, 
        AdminsController adminsController, AdminUsers adminUsers)
    {
        _botCommandsFactory = botCommandsFactory;
        _adminsController = adminsController;
        _adminUsers = adminUsers;
        BotClient = botClient;
    }

    public void SetCommandPath(string path)
    {
        MyPath = path;
    }

    public virtual async Task<CommandResult> ProcessMessage(Update update, string[]? args, string? reroutedForPath = null)
    {
        switch (args.Length)
        {
            case 0:
            {   
                // just enter to the command
                await ShowSubCommands(update);
                break;
            }
            default:
            {
                // all other cases - go to subsommand
                await ShowSubCommands(update, $"Не знайдена підкоманда `{args[1]}`");
                break;
            }
        }
        
        return CommandResult.Ok;
    }
    
    protected Task ShowSubCommands(Update update, string? extMessage = "")
    {
        return ComposeMessage(update)
            .SetText(extMessage == null ? "Виберіть команду." : $"{extMessage}\nВиберіть команду.")
            .AddButtons(_botCommandsFactory.GetChildren(this, _adminUsers.GetAccessLevel(update.GetChatId())).Select(e => (e.Item1.CommandName, e.route)).ToArray())
            .SetNeedUpMenuButton()
            .SetNeedMainMenuButton()
            .Send();
    }
    
    protected Task PromptUserDialog(Update update, string message, string acceptRoute, string declineRoute = null)
    {
        return ComposeMessage(update)
            .SetText(message)
            .AddButton("✅ Так", acceptRoute)
            .AddButton("⛔ Ні", declineRoute ?? (_botCommandsFactory.GetMainMenuCommand()).route)
            .Send();
    }
    
    
    protected Task PromptUserDialogForCurrentPath(Update update, string message, string acceptArgs, string declineArgs = null)
    {
        return ComposeMessage(update)
            .SetText(message)
            .AddButtonForCurrentPath("✅ Так", acceptArgs)
            .AddButtonForCurrentPath("⛔ Ні", declineArgs ?? (_botCommandsFactory.GetMainMenuCommand()).route)
            .Send();
    }
    
    protected Task SendMessageWithButtons(Update update, string message, 
        int buttonsInRow = 2, 
        bool addBackToMainCommandButton = false, 
        bool addBackToManuButton = false,
        params (string label, string route)[] buttons)
    {
        return ComposeMessage(update)
            .SetText(message)
            .SetButtonsInARow(buttonsInRow)
            .SetNeedMainMenuButton(addBackToManuButton)
            .SetNeedCurrentMenuButton(addBackToMainCommandButton)
            .AddButtons(buttons)
            .Send();
    }
    
    public BotCommandMessageComposer ComposeMessage(Update replyTo)
    {
        BotCommandMessageComposer composer = new BotCommandMessageComposer(MyPath, _botCommandsFactory, async c =>
        {
            var user = await _adminsController.GetAdminUser(replyTo.GetChatId());
            
            var buttons = new List<BotCommandMessageComposer.MessageCallbackButton>();
            foreach (var button in c.Buttons)
            {
                if (button.CallbackData == BotCommandMessageComposer.PlaceChildButtonsHere)
                {
                    var children = _botCommandsFactory.GetChildren(this, user.Data.BotAccessLevel);
                    foreach (var (childCommand, route) in children)
                    {
                        buttons.Add(new BotCommandMessageComposer.MessageCallbackButton()
                        {
                            Text = childCommand.CommandName,
                            CallbackData = route
                        });
                    }
                }
                else
                {
                    buttons.Add(button);
                }
            }
            var rows = buttons
                .Select((e,index) => new {
                    Key = InlineKeyboardButton.WithCallbackData(e.Text, e.CallbackData),
                    Group = (index/c.ButtonsInARow)*c.ButtonsInARow})
                .GroupBy(b=>b.Group)
                .Select(g=>g.Select(i=>i.Key).ToArray())
                .ToList();
            
            if (c.NeedMainMenuButton || c.NeedCurrentMenuButton || c.NeedUpMenuButton)
            {
                var back = new List<InlineKeyboardButton>();
            
                if (c.NeedCurrentMenuButton)
                {
                    back.Add(InlineKeyboardButton.WithCallbackData(this.CommandName, this.MyPath));
                }

                if (c.NeedUpMenuButton && ParentCommandType != null)
                {
                    var upMenu = _botCommandsFactory.GetCommand(ParentCommandType);
                    if (upMenu != null)
                    {
                        back.Add(InlineKeyboardButton.WithCallbackData(upMenu.Value.Item1.CommandName, upMenu.Value.route));
                    }
                }

                if (c.NeedMainMenuButton)
                {
                    var mainMenu = _botCommandsFactory.GetMainMenuCommand();
                    if ((mainMenu.command != this || !c.NeedUpMenuButton) &&
                        (mainMenu.command.GetType() != ParentCommandType || !c.NeedUpMenuButton))
                    {
                        back.Add(InlineKeyboardButton.WithCallbackData(mainMenu.command.CommandName, mainMenu.route));
                    }
                }
            
                rows.Add(back.ToArray());
            }

            await user!.SendMessage(c.Text,
                replyMarkup: new InlineKeyboardMarkup(rows),
                route: MyPath,
                parseMode: c.ParseMode,
                replyToMessageId: replyTo.GetMessageId(), alsoRemoveThisMessagesAtRouteExit: c.AlsoRemoveThisMessagesAtRouteExit.Count == 0
                    ? null
                    : c.AlsoRemoveThisMessagesAtRouteExit.ToArray());
        });
        
        return composer;
    }
    
    protected Task SendTextMessageWithDefaultCommandButton(Update update, string message)
    {
        return SendMessageWithButtons(update, message, 2, true, true);
    }
}