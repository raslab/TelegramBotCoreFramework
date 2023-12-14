using System.Diagnostics;
using Telegram.Bot.Types.Enums;

namespace TG.UpdatesProcessing.BotCommands;

public class BotCommandMessageComposer
{
    public const string PlaceChildButtonsHere = "PlaceChildButtonsHere";
    
    public class MessageCallbackButton
    {
        public string Text { get; set; }
        public string CallbackData { get; set; }
    }
        
    public string Text { get; private set; }
    public List<MessageCallbackButton> Buttons { get; private set; } = new List<MessageCallbackButton>();
    public bool NeedMainMenuButton { get; private set; } = false;
    public bool NeedCurrentMenuButton { get; private set; } = false;
    public bool NeedUpMenuButton { get; private set; } = false;
    public int ButtonsInARow { get; private set; } = 2;
    public List<int> AlsoRemoveThisMessagesAtRouteExit { get; private set; } = new List<int>();
    public ParseMode ParseMode { get; private set; } = Telegram.Bot.Types.Enums.ParseMode.Html;

    private readonly string _currentPath;
    private readonly IBotCommandsFactory _botCommandsFactory;
    private readonly Func<BotCommandMessageComposer,Task> _callback;

    public BotCommandMessageComposer(string currentPath, IBotCommandsFactory botCommandsFactory, Func<BotCommandMessageComposer,Task> callback)
    {
        _currentPath = currentPath;
        _botCommandsFactory = botCommandsFactory;
        _callback = callback;
    }

    public Task Send()
    {
        return _callback(this);
    }

    public BotCommandMessageComposer SetText(string message)
    {
        Text = message;
        return this;
    }
        
    public BotCommandMessageComposer SetNeedMainMenuButton(bool need = true)
    {
        NeedMainMenuButton = need;
        return this;
    }
        
    public BotCommandMessageComposer SetNeedCurrentMenuButton(bool need = true)
    {
        NeedCurrentMenuButton = need;
        return this;
    }
        
    public BotCommandMessageComposer SetNeedUpMenuButton(bool need = true)
    {
        NeedUpMenuButton = need;
        return this;
    }

    public BotCommandMessageComposer SetButtonsInARow(int count)
    {
        ButtonsInARow = count;
        return this;
    }

    public BotCommandMessageComposer AddButton(string text, string path, params string[] args)
    {
        Buttons.Add(new MessageCallbackButton()
        {
            Text = text,
            CallbackData = $"{path}?{string.Join("/", args)}"
        });
        return this;
    }
        
    public BotCommandMessageComposer AddButtonForCurrentPath(string text, params string[] args)
    {
        Buttons.Add(new MessageCallbackButton()
        {
            Text = text,
            CallbackData = $"{_currentPath}?{string.Join("/", args)}"
        });
        return this;
    }
        
    public BotCommandMessageComposer AddButton(string text, string callbackData)
    {
        Buttons.Add(new MessageCallbackButton()
        {
            Text = text,
            CallbackData = callbackData
        });
        return this;
    }
        
    public BotCommandMessageComposer AddButtons(params (string label, string route)[] buttons)
    {
        foreach (var (label, route) in buttons)
        {
            AddButton(label, route);
        }
        return this;
    }
        
    public BotCommandMessageComposer AddButtonsForCurrentPath(params(string text, string args)[] buttons)
    {
        foreach (var (label, route) in buttons)
        {
            AddButtonForCurrentPath(label, route);
        }
        return this;
    }
        
    public BotCommandMessageComposer AddButtonsForCurrentPath(params(string text, string[] args)[] buttons)
    {
        foreach (var (label, route) in buttons)
        {
            AddButtonForCurrentPath(label, route);
        }
        return this;
    }

    public BotCommandMessageComposer AddButtonForPath<T>(string text, params string[] args) 
        where T:IBotCommand
    {
        var command = _botCommandsFactory.GetCommand(typeof(T));
        Debug.Assert(command != null, nameof(command) + " != null");
        Buttons.Add(new MessageCallbackButton()
        {
            Text = text,
            CallbackData = $"{command.Value.route}?{string.Join("/", args)}"
        });
        return this;
    }
    
    public BotCommandMessageComposer RegisterMessageIdToRemoveAtPathExit(params int[] args)
    {
        this.AlsoRemoveThisMessagesAtRouteExit.AddRange(args);
        return this;
    }

    public BotCommandMessageComposer SetMarkdown(string text)
    {
        ParseMode = ParseMode.Markdown;
        return SetText(text);
    }

    public BotCommandMessageComposer AddChildrenButtons()
    {
        AddButton("<hidden>", PlaceChildButtonsHere);
        return this;
    }
}