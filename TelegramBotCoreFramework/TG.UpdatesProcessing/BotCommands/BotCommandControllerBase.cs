using Helpers.AdminsCommunication;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TG.UpdatesProcessing.BotCommands;

public abstract class BotCommandControllerBase : BotCommandBase
{
    public delegate Task<CommandResult> ShortcutCallback(Update update, string[]? args,
        string? reroutedForPath = null);
    
    private ShortcutCallback? _defaultCallback = null;
    private readonly Dictionary<string, ShortcutCallback> _callbacks = new Dictionary<string, ShortcutCallback>();
    private bool _built = false;

    protected BotCommandControllerBase(TelegramBotClient botClient, IBotCommandsFactory botCommandsFactory,
        AdminsController adminsController, AdminUsers adminUsers)
        : base(botClient, botCommandsFactory, adminsController, adminUsers)
    {
    }

    protected void AddDefaultShortcut(ShortcutCallback callback)
    {
        _defaultCallback = callback;
    }

    protected void AddArgShortcut(string firstArgument, ShortcutCallback callback)
    {
        _callbacks.Add(firstArgument, callback);
    }

    public override async Task<CommandResult> ProcessMessage(Update update, string[]? args,
        string? reroutedForPath = null)
    {
        if (!_built)
        {
            await Build();
            if (_defaultCallback == null)
                _defaultCallback = (update1, args1, reroutedForPath1) => base.ProcessMessage(update1, args1, reroutedForPath1);
            _built = true;
        }
        
        switch (args.Length)
        {
            case 0:
            {
                return await _defaultCallback(update, args, reroutedForPath);
            }
            default:
            {
                var key = args[0];
                var task = _callbacks[key];
                return await task(update, args, reroutedForPath);
            }
        }
    }

    protected abstract Task Build();
}