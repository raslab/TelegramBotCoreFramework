using System.Text;
using Google.Cloud.Firestore;
using Helpers;
using Helpers.AdminsCommunication;
using Helpers.PredefinedChannels;

namespace TG.UpdatesProcessing.BotCommands;

public interface IBotCommandsFactory
{
    (IBotCommand command, string route) GetMainMenuCommand();
    IBotCommand? FindCommandByPath(string path, CommandsAccessLevel accessLevel = CommandsAccessLevel.None);
    (IBotCommand, string route)[]? GetChildren(IBotCommand command,
        CommandsAccessLevel accessLevel = CommandsAccessLevel.None);
    (IBotCommand, string route)? GetCommand(Type? commandType);
}

public class BotCommandsFactoryInitiator
{
    private readonly IEnumerable<IBotCommand> _commands;
    private readonly IBotCommandsFactory _factory;

    public BotCommandsFactoryInitiator(IEnumerable<IBotCommand> commands, IBotCommandsFactory factory)
    {
        _commands = commands;
        _factory = factory;
    }

    public bool Inited { get; private set; } = false;

    public Task Init()
    {
        return (_factory as BotCommandsFactory).Init(_commands.ToArray()).ContinueWith(_ => { Inited = true;});
    }
}

internal class BotCommandsFactory : IBotCommandsFactory
{
    [FirestoreData]
    private class CommandIndex 
    {
        [FirestoreProperty] public string TypeName { get; set; }
        [FirestoreProperty] public string Path { get; set; }
        [FirestoreProperty] public string CommandId { get; set; }
    }

    [FirestoreData]
    private class CommandIndexes
    {
        [FirestoreProperty] public List<CommandIndex> Indexes { get; set; } = new List<CommandIndex>();
    }
    
    private readonly ConfigurationStorage _configurationStorage;
    private readonly LoggingChannel _loggingChannel;

    private IBotCommand[] _commands;
    private CommandIndexes? _indexes = null;
    private Dictionary<string, IBotCommand> _commandsMap = null;
    private Dictionary<IBotCommand, string> _commandsPathsMap = null;
    private IBotCommand _mainMenuCommand;

    public BotCommandsFactory(ConfigurationStorage configurationStorage,
        LoggingChannel loggingChannel)
    {
        _configurationStorage = configurationStorage;
        _loggingChannel = loggingChannel;
    }

    public async Task Init(IBotCommand[] commands)
    {
        _commands = commands;
        if (_indexes != null && _commandsMap != null)
            return;

        try
        {
            _indexes = await _configurationStorage.Get<CommandIndexes>() ?? new CommandIndexes();
            var startIndex = _indexes.Indexes.Count;
        
            var needAdd = new List<CommandIndex>();
            foreach (var c in _commands)
            {
                bool allNotMatch = true;

                foreach (var i in _indexes.Indexes)
                {
                    if (i.TypeName == c.GetType().FullName)
                    {
                        allNotMatch = false;
                        break;
                    }
                }

                if (allNotMatch)
                {
                    needAdd.Add(new CommandIndex()
                    {
                        TypeName = c.GetType().FullName,
                        Path = GetPath(c),
                        CommandId = $"{(startIndex++).ToString()}"
                    });
                }
            }
        
            if (needAdd.Any())
            {
                _indexes.Indexes.AddRange(needAdd);
                await _configurationStorage.Push(_indexes);
            }

            _commandsMap =
                _commands.ToDictionary(c => _indexes.Indexes.First(i => i.TypeName == c.GetType().FullName).CommandId, c => c);
            _commandsPathsMap =
                _commands.ToDictionary(c => c, c => _indexes.Indexes.First(i => i.TypeName == c.GetType().FullName).CommandId);
            _mainMenuCommand = _commands.First(c => c.CommandName == MainMenuBotCommand.MainMenuCommandName);
        
            foreach (var (path, command) in _commandsMap)
            {
                command.SetCommandPath(path);
            }
        }
        catch (Exception e)
        {
            await _loggingChannel.LogExceptionToServiceChannel("Помилка під час генерування мапи роутів.", e);
        }
    }

    private string GetPath(IBotCommand botCommand)
    {
        Action<IBotCommand, StringBuilder> rec = null;
        rec = (IBotCommand com, StringBuilder builder) =>
        {
            if (com.ParentCommandType != null)
            {
                var p = _commands.First(c => c.GetType() == com.ParentCommandType);
                rec(p, builder);
                builder.Append("/");
            }

            builder.Append(com.CommandName);
        };
        var builder = new StringBuilder();
        rec(botCommand, builder);
        return builder.ToString();
    }

    public (IBotCommand command, string route) GetMainMenuCommand()
    {
        return (_mainMenuCommand, _commandsPathsMap[_mainMenuCommand]);
    }

    public IBotCommand? FindCommandByPath(string path, CommandsAccessLevel accessLevel = CommandsAccessLevel.None)
    {
        return _commandsMap.ContainsKey(path) && (_commandsMap[path].AccessLevel <= accessLevel || _commandsMap[path].AccessLevel == CommandsAccessLevel.Hidden)
            ? _commandsMap[path]
            : null;

    }

    public (IBotCommand, string route)[]? GetChildren(IBotCommand command, CommandsAccessLevel accessLevel = CommandsAccessLevel.None)
    {
        var t = command.GetType();
        return _commands.Where(c => c.ParentCommandType != null && c.ParentCommandType == t && c.AccessLevel <= accessLevel)
            .Select(c=>(c,_commandsPathsMap[c]))
            .ToArray();
    }

    public (IBotCommand, string route)? GetCommand(Type? commandType)
    {
        if (commandType == null) return null;
        var com = _commands.FirstOrDefault(c => c.GetType() == commandType);
        if (com == null) return null;
        return (com, _commandsPathsMap[com]);
    }
}