using CommunicationChat;
using CommunicationChat.BotPrivateCommunication;
using Helpers;
using Helpers.AdminsCommunication;
using Helpers.Extensions;
using Helpers.PredefinedChannels;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TG.UpdatesProcessing.BotCommands;
using TG.UpdatesProcessing.Helpers;
using TG.UpdatesProcessing.WelcomeBot;

namespace TG.UpdatesProcessing;

public class BotUpdateProcessing
{
    private TelegramBotClient BotClient {get; set;}
    private ILogger Log {get; set;}
    private readonly IUserInputAwaiting _userInputAwaiting;
    private readonly ChannelJoinRequestsProcessor _channelJoinRequestsProcessor;
    private readonly IBotCommandsFactory _botCommandsFactory;
    private readonly BotCommandsFactoryInitiator _factoryInitiator;
    private readonly LoggingChannel _loggingChannel;
    private readonly AdminUsers _adminUsers;
    private readonly IEnumerable<IBotUpdateThirdPartyProcessor> _thirdPartyProcessors;
    private readonly SupportBotProxyFactory _supportBotProxyFactory;
    private readonly ChannelsSettings _channelsSettings;

    public BotUpdateProcessing(ILogger<BotUpdateProcessing> logger, 
        TelegramBotClient botClient,  
        IUserInputAwaiting userInputAwaiting,
        ChannelJoinRequestsProcessor channelJoinRequestsProcessor,
        IBotCommandsFactory botCommandsFactory,
        BotCommandsFactoryInitiator factoryInitiator,
        LoggingChannel loggingChannel,
        AdminUsers adminUsers,
        IEnumerable<IBotUpdateThirdPartyProcessor> thirdPartyProcessors,
        SupportBotProxyFactory supportBotProxyFactory,
        ChannelsSettings channelsSettings)
    {
        BotClient = botClient;
        Log = logger;
        this._userInputAwaiting = userInputAwaiting;
        this._channelJoinRequestsProcessor = channelJoinRequestsProcessor;
        _botCommandsFactory = botCommandsFactory;
        _factoryInitiator = factoryInitiator;
        _loggingChannel = loggingChannel;
        _adminUsers = adminUsers;
        _thirdPartyProcessors = thirdPartyProcessors;
        _supportBotProxyFactory = supportBotProxyFactory;
        _channelsSettings = channelsSettings;
    }

    public async Task ProcessMessage(Update update)
    {
        if (_thirdPartyProcessors?.Any() ?? false)
        {
            foreach (var processor in _thirdPartyProcessors)
            {
                try
                {
                    if (await processor.Process(update) == ProcessResult.StopProcessing)
                        return;
                }
                catch (Exception e)
                {
                    Log.LogCritical($"Помилка під час зовнішнього процесінгу.\n{e.Message}\n{e.StackTrace}");
                    await _loggingChannel.LogExceptionToServiceChannel($"Помилка під час зовнішнього процесінгу.", e);
                }
            }
        }

        if ((update.Type == UpdateType.Message && _adminUsers.IsManager(update.Message.From.Id) && update.Message.Chat.Type == ChatType.Private
             || (update.Type == UpdateType.CallbackQuery && _adminUsers.IsManager(update.CallbackQuery.From.Id) && update.CallbackQuery.Message?.Chat.Type == ChatType.Private)))
        {
            await ProcessPrivateMessageFromAdministrator(update);
        }
        else if (update.Type == UpdateType.ChatJoinRequest)
        {
            await _channelJoinRequestsProcessor.JoinRequestHandle(update.ChatJoinRequest);
        }
        else if (update.Type == UpdateType.MyChatMember)
        {
            await _channelJoinRequestsProcessor.UpdateUserStatus(update.MyChatMember);
        }
        else
        {
            // looks like it's some general comunication with bot
            await _channelsSettings.LoadSchedule(); 
            await (await _supportBotProxyFactory.CreateDefault()).ProcessGenericCommunication(update, BotClient);
        }
    }

    private async Task ProcessPrivateMessageFromAdministrator(Update update)
    {
        if (!_factoryInitiator.Inited)
        {
            try
            {
                await _factoryInitiator.Init();
            }
            catch (Exception e)
            {
                Log.LogCritical($"Помилка під час ініціалізації фабрики команд.\n{e.Message}\n{e.StackTrace}");
                await _loggingChannel.LogExceptionToServiceChannel($"Помилка під час ініціалізації фабрики команд.",e);
            }
        }

        if (update.Type != UpdateType.Message && update.Type != UpdateType.CallbackQuery)
        {
            await _loggingChannel.LogMessageToServiceChannel(
                $"Наразі бот не підтримує обробку повідомлень типу {update.Type} при спілкуванні із адміністраторами. " +
                $"Якщо це не помилка, зверніться до сервісу підтримки. Інакше - ігноруйте, це ніяк не вплине на роботу бота.");
            return;
        }
        
        var chatId = update.GetChatId();
        var redirectPath = await _userInputAwaiting.GetMessageRedirectRouteIfExists(chatId, true);

        var accessLevel = _adminUsers.GetUser(chatId)!.BotAccessLevel;
        var route = update?.CallbackQuery?.Data ?? redirectPath;
        var routeParts = route?.Split('?') ?? new []{"No route"};
        var path = routeParts[0];
        var args = routeParts.Length==2 ? routeParts[1].Split('/') : Array.Empty<string>();

        var command = _botCommandsFactory.FindCommandByPath(path, accessLevel);
        if (command == null)
        {
            redirectPath = path;
            command = _botCommandsFactory.GetMainMenuCommand().command;
        }

        if (redirectPath == AdminsController.BackToMainMenuCommandPath)
        {
            redirectPath = null;
        }
        
        try
        {
            var res = await command.ProcessMessage(update, args, redirectPath);
            if (res == CommandResult.ShowMainMenu)
            {
                await _botCommandsFactory.GetMainMenuCommand().command.ProcessMessage(update, Array.Empty<string>());
            }
        }
        catch (Exception e)
        {
            Log.LogCritical($"Помилка під час виконання команди {command.CommandName}\n{e.Message}\n{e.StackTrace}");
            await _loggingChannel.LogExceptionToServiceChannel($"Помилка під час виконання команди {command.CommandName}",e);
        }
    }
}