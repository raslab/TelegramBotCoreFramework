using Analytics.UsersDatabase;
using CommunicationChat.MassSendings;
using Helpers.Extensions;
using Helpers.PredefinedChannels;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace CommunicationChat.BotPrivateCommunication;

public class WelcomeBotCommunicationFactory
{
    private readonly LoggingChannel _loggingChannel;
    private readonly MassMessageSendingFactory _massMessageSendingFactory;
    private readonly MassMessagesDeletingFactory _massMessagesDeletingFactory;
    private readonly SubscribersDatabase _subscribersDatabase;
    private readonly WelcomeBotSettings _welcomeBotSettings;
    private readonly TelegramBotClient _telegramBotClient;
    
    private readonly Dictionary<long, WelcomeBotCommunicationProcessor> _cache = new Dictionary<long, WelcomeBotCommunicationProcessor>();

    public WelcomeBotCommunicationFactory(LoggingChannel loggingChannel,
        MassMessageSendingFactory massMessageSendingFactory, 
        MassMessagesDeletingFactory massMessagesDeletingFactory,
        SubscribersDatabase subscribersDatabase, WelcomeBotSettings welcomeBotSettings, 
        TelegramBotClient telegramBotClient)
    {
        _loggingChannel = loggingChannel;
        _massMessageSendingFactory = massMessageSendingFactory;
        _massMessagesDeletingFactory = massMessagesDeletingFactory;
        _subscribersDatabase = subscribersDatabase;
        _welcomeBotSettings = welcomeBotSettings;
        _telegramBotClient = telegramBotClient;
    }
    
    public WelcomeBotCommunicationProcessor GetServiceFor(IProxyChannelSubscribersRepository subscribersRepository,
        WelcomeBotSettings welcomeBotSettings, TelegramBotClient telegramBotClient)
    {
        if (_cache.TryGetValue(telegramBotClient.BotId!.Value, out var service))
        {
            if (service.Settings == welcomeBotSettings)
                return service;
            _cache.Remove(telegramBotClient.BotId!.Value);
        }

        service = new WelcomeBotCommunicationProcessor(_loggingChannel, _massMessageSendingFactory, _massMessagesDeletingFactory);
        service.InitFor(subscribersRepository, welcomeBotSettings, telegramBotClient);
        _cache.Add(telegramBotClient.BotId!.Value, service);
        return service;
    }
    
    public WelcomeBotCommunicationProcessor GetDefault()
    {
        return GetServiceFor(_subscribersDatabase, _welcomeBotSettings, _telegramBotClient);
    }
}

public interface IWelcomeBotCommunicationProcessor
{
    Task UserStartMessageHandle(Update update, IProxyChannelSubscriber? proxyChannelSubscriber);
}

public class WelcomeBotCommunicationProcessor : IWelcomeBotCommunicationProcessor
{
    private readonly LoggingChannel _loggingChannel;
    private readonly MassMessageSendingFactory _massMessageSendingFactory;
    private readonly MassMessagesDeletingFactory _massMessagesDeletingFactory;
    
    private WelcomeBotSettings _welcomeBotSettings;
    private IProxyChannelSubscribersRepository _subscribersDatabase;
    private MassMessagesDeletingService _massMessagesDeletingService;
    private MassMessageSendingService _massMessageSendingService;

    public WelcomeBotCommunicationProcessor(LoggingChannel loggingChannel,
        MassMessageSendingFactory massMessageSendingFactory, 
        MassMessagesDeletingFactory massMessagesDeletingFactory)
    {
        _loggingChannel = loggingChannel;
        _massMessageSendingFactory = massMessageSendingFactory;
        _massMessagesDeletingFactory = massMessagesDeletingFactory;
    }

    public WelcomeBotSettings Settings => _welcomeBotSettings;

    public void InitFor(IProxyChannelSubscribersRepository subscribersRepository, 
        WelcomeBotSettings welcomeBotSettings, TelegramBotClient telegramBotClient)
    {
        _subscribersDatabase = subscribersRepository;
        _welcomeBotSettings = welcomeBotSettings;

        _massMessagesDeletingService = _massMessagesDeletingFactory.CreateServiceFor(telegramBotClient);
        _massMessageSendingService = _massMessageSendingFactory.CreateFor(telegramBotClient);
    }
    
    public async Task UserStartMessageHandle(Update update, IProxyChannelSubscriber? sub)
    {
        await _welcomeBotSettings.LoadDefaultIfNeeded();
        if (sub == null) 
            sub = await _subscribersDatabase.GetSubscriber(update.GetChatId());

        if (sub == null)
        {
            sub = InitSubFromStartMessage(update.Message);
        }
        
        var captcha = sub.MessagesHistory?.FirstOrDefault(m => m.MessageType == MessageType.Captcha);
        if (captcha != null)
        {
            try
            {
                await _massMessagesDeletingService.EnqueueMessage((sub.Id, update.GetMessageId()));
                await _massMessagesDeletingService.EnqueueMessage((sub.Id, captcha.MessageId));
            }
            catch
            {
                // ignored
            }

            sub.MessagesHistory.Remove(captcha);
            sub.CaptchaStatus = CaptchaStatus.Passed;
        }
        
        if (sub.MessagesHistory?.All(m=>m.MessageType != MessageType.Welcome) ?? true)
        {
            if (sub.MessagesHistory == null)
                sub.MessagesHistory = new List<MessageDetail>();
            
            // let's try send welcome sequence
            try
            {
                foreach (var msg in _welcomeBotSettings.WelcomeSequence)
                {
                    if (msg==null)
                        continue;
                    var m = await _massMessageSendingService.EnqueueMessage(
                        new MassMessageSendingService.MessageRequest(msg, sub.Id));
                    sub.MessagesHistory.Add(new MessageDetail()
                    {
                        MessageId = m.MessageId,
                        MessageType = MessageType.Welcome,
                        SentTime = DateTime.UtcNow.ToFirestoreTimestamp()
                    });
                }
            }
            catch (Exception e)
            {
                if (e.Message.Contains("Forbidden: bot was blocked by the user"))
                {
                    sub.IsBotBlockedByUser = true;
                }
                else if (e.Message.Contains("bot can't initiate conversation with a user"))
                {
                    sub.IsBotBlockedByUser = true;
                }
                else
                    await _loggingChannel.LogExceptionToServiceChannel(
                        "Помилка під час опрацьування повідомлення 'start' від користувача. Це не вплине на користувача або на його підписку.",
                        e);
            }
        }

        await _subscribersDatabase.UpdateSubscriber(sub);
    }
    
    

    private SubscriberDto InitSubFromStartMessage(Message message)
    {
        return new SubscriberDto()
        {
            Id = message.From.Id,
            Language = message.From.LanguageCode,
            UserName = message.From.Username,
            LastName = message.From.LastName,
            FirstName = message.From.FirstName,
            RegistrationDate = DateTime.UtcNow.ToFirestoreTimestamp(),
            RegistrationSource = SubscriberCameFrom.OrganicFromBot,
            CaptchaStatus = CaptchaStatus.Passed,
            LastDelivery = DateTime.UtcNow.ToFirestoreTimestamp(),
            IsBotBlockedByUser = false
        };
    }

}