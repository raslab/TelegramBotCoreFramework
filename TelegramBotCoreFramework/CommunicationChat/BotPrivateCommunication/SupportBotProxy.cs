using System.Diagnostics;
using Analytics.UsersDatabase;
using Helpers;
using Helpers.Extensions;
using Helpers.PredefinedChannels;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using MessageType = Telegram.Bot.Types.Enums.MessageType;

namespace CommunicationChat.BotPrivateCommunication;

public class SupportBotProxyFactory
{
    private readonly TelegramBotClient _botClient;
    private readonly LoggingChannel _loggingChannel;
    private readonly IProxyChannelSubscribersRepository _subscribersRepository;
    private readonly WelcomeBotCommunicationFactory _welcomeBotCommunicationFactory;
    private readonly SupportProxyChannelHolderFactory _supportProxyChannelHolderFactory;
    private readonly WelcomeBotSettings _welcomeBotSettings;
    private readonly ChannelsSettings _channelsSettings;
    private readonly TelegramBotClientsFactory _telegramBotClientsFactory;

    private readonly Dictionary<WelcomeBotSettings, SupportBotProxy> _cache =
        new Dictionary<WelcomeBotSettings, SupportBotProxy>();

    public SupportBotProxyFactory(TelegramBotClient botClient, LoggingChannel loggingChannel, 
        SubscribersDatabase subscribersRepository, WelcomeBotCommunicationFactory welcomeBotCommunicationFactory,
        SupportProxyChannelHolderFactory supportProxyChannelHolderFactory,
        WelcomeBotSettings welcomeBotSettings, ChannelsSettings channelsSettings,
        TelegramBotClientsFactory telegramBotClientsFactory)
    {
        _botClient = botClient;
        _loggingChannel = loggingChannel;
        _subscribersRepository = subscribersRepository;
        _welcomeBotCommunicationFactory = welcomeBotCommunicationFactory;
        _supportProxyChannelHolderFactory = supportProxyChannelHolderFactory;
        _welcomeBotSettings = welcomeBotSettings;
        _channelsSettings = channelsSettings;
        _telegramBotClientsFactory = telegramBotClientsFactory;
    }

    public SupportBotProxy CreateFor(WelcomeBotSettings welcomeBotSettings, IProxyChannelSubscribersRepository subscribersRepository)
    {
        if (_cache.TryGetValue(welcomeBotSettings, out var proxy))
        {
            return proxy;
        }
        
        var client = _telegramBotClientsFactory.GetClientFor(welcomeBotSettings.BotAccessToken!);
        var supportBotProxy = new SupportBotProxy(_supportProxyChannelHolderFactory, _loggingChannel); 
        var processor = _welcomeBotCommunicationFactory.GetServiceFor(subscribersRepository, welcomeBotSettings, client);
        supportBotProxy.InitiateFor(welcomeBotSettings.ProxyChat?.ChannelId, client, subscribersRepository, processor);
        _cache.Add(welcomeBotSettings, supportBotProxy);
        return supportBotProxy;
    }

    public async Task<SupportBotProxy> CreateDefault()
    {
        await _welcomeBotSettings.LoadDefaultIfNeeded();
        if (_welcomeBotSettings.ProxyChat == null)
        {
            await _channelsSettings.LoadSchedule();
            _welcomeBotSettings.BotUserId = _botClient.BotId!.Value;
            _welcomeBotSettings.BotAccessToken = Env.TelegramBotToken;
            _welcomeBotSettings.ProxyChat = _channelsSettings.CommunicationChannel;
            await _welcomeBotSettings.SaveSettings();
        }

        return CreateFor(_welcomeBotSettings, _subscribersRepository);
    }

    public void ClearCache()
    {
        _cache.Clear();
    }
}

public class SupportBotProxy
{
    private readonly SupportProxyChannelHolderFactory _supportProxyChannelHolderFactory;
    private readonly LoggingChannel _loggingChannel;
    
    private IProxyChannelSubscribersRepository _subscribersDatabase;
    private SupportProxyChannelHolder _supportProxyChannelHolder;
    private long? _supportChatId;
    private IWelcomeBotCommunicationProcessor _channelJoinRequestsProcessor;

    public SupportBotProxy(
        SupportProxyChannelHolderFactory supportProxyChannelHolderFactory,
        LoggingChannel loggingChannel)
    {
        _supportProxyChannelHolderFactory = supportProxyChannelHolderFactory;
        _loggingChannel = loggingChannel;
    }


    public void InitiateFor(long? supportChatId, TelegramBotClient botClient, IProxyChannelSubscribersRepository subscribersRepository, 
        IWelcomeBotCommunicationProcessor welcomeBotCommunicationProcessor)
    {
        _supportChatId = supportChatId;
        _subscribersDatabase = subscribersRepository;
        _supportProxyChannelHolder = _supportProxyChannelHolderFactory.Create(supportChatId, botClient, subscribersRepository);
        _channelJoinRequestsProcessor = welcomeBotCommunicationProcessor;
    }

    public async Task ProcessGenericCommunication(Update update, TelegramBotClient botClient)
    {
        try
        {
            if (update.Message?.Chat?.Id == _supportChatId)
            {
                // looks like it's admin answering to user
                try
                {
                    if (update.Message.Type != MessageType.ForumTopicClosed
                        && update.Message.Type != MessageType.ForumTopicCreated
                        && update.Message.Type != MessageType.ForumTopicEdited
                        && update.Message.Type != MessageType.ForumTopicReopened)
                    {
                        await ReplyToUserMessage(botClient, update);
                    }
                }
                catch (Exception e)
                {
                    if (e.Message.Contains("bot can't initiate conversation with a user"))
                    {
                        await botClient.SendTextMessageAsync(
                            _supportChatId,
                            $"Користувач заблокував бота, не можу йому відповісти.",
                            parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                            replyToMessageId: update.ChannelPost.MessageId
                        );
                    }
                    // throw;
                }
                
                return;
            }
            
            switch (update.Type)
            {
                case Telegram.Bot.Types.Enums.UpdateType.Message:
                    if (update.Message.Chat.Type == ChatType.Private)
                        await ForwardToCommunicationChannel(botClient, update);
                    break;
                case Telegram.Bot.Types.Enums.UpdateType.EditedMessage:
                    if (update.EditedMessage.Chat.Type == ChatType.Private)
                    {
                        await _supportProxyChannelHolder.SendMessageToCommunicationChannel(
                            "Користувач відкорегував наступне повідомлення. Пересилаю свіжу версію.",
                            update.EditedMessage.From);
                        await ForwardToCommunicationChannel(botClient, update);
                    }
                    break;
                case Telegram.Bot.Types.Enums.UpdateType.EditedChannelPost:
                    if (update.EditedChannelPost.Chat.Id == _supportChatId)
                    {
                        await botClient.SendTextMessageAsync(
                            chatId:_supportChatId,
                            messageThreadId: update.EditedMessage.MessageThreadId,
                            text: $"Нажаль, відправка редагованих повідомлень не підтримується для відповіді контактам. Якщо ви помилились в граматиці чи щось подібне - скоріш за все, для користувача буде ок якщо використати наступний формат:\n<code>* виправлений текст</code>",
                            parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                            replyToMessageId: update.EditedChannelPost.MessageId
                        );
                    }
                    break;

            }
        }
        catch (Exception e)
        {
            await _loggingChannel.LogExceptionToServiceChannel("Помилка при процессінгу комунікації.",e);
        }
    }

    private async Task ReplyToUserMessage(TelegramBotClient botClient, Update update)
    {
        Debug.Assert(update.Message != null, "update.Message != null");
        var sub = await _subscribersDatabase.GetSubscriberForCommunicationChannel(update.Message.MessageThreadId.Value);
        await botClient.SendMessageToChannel(message: update.Message, sub.Id);
    }

    private async Task ForwardToCommunicationChannel(TelegramBotClient botClient, Update update)
    {
        var sub = await _subscribersDatabase.GetSubscriber(update.GetChatId());
        if (sub == null || !(sub.MessagesHistory?.Any() ?? false) || sub.MessagesHistory.All(m=>m.MessageType != Analytics.UsersDatabase.MessageType.Welcome))
        {
            await _channelJoinRequestsProcessor.UserStartMessageHandle(update, sub);
        }
        else 
        {
            if (!_supportProxyChannelHolder.CommunicationChannelSettedUp)
            {
                _supportProxyChannelHolder = _supportProxyChannelHolderFactory.Create(_supportChatId, botClient, _subscribersDatabase);
            }
            await _supportProxyChannelHolder.ForwardMessageToCommunicationChannel(update, sub);
        }
    }
}