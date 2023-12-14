using System.Collections.Concurrent;
using System.Diagnostics;
using Helpers;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace CommunicationChat.MassSendings;

public class MassMessageSendingFactory : IHostedService, IDisposable
{
    private readonly Dictionary<TelegramBotClient, MassMessageSendingService> _cache =
        new Dictionary<TelegramBotClient, MassMessageSendingService>();

    private readonly TelegramBotClient _telegramBotClient;
    private bool _started = false;

    public MassMessageSendingFactory(TelegramBotClient telegramBotClient)
    {
        _telegramBotClient = telegramBotClient;
    }

    public MassMessageSendingService CreateFor(TelegramBotClient telegramBotClient)
    {
        if (_cache.TryGetValue(telegramBotClient, out var service))
            return service;

        service = new MassMessageSendingService();
        service.InitFor(telegramBotClient);
        _cache.Add(telegramBotClient, service);
        if (_started)
            service.StartAsync(default).Wait();
        return service;
    }

    public MassMessageSendingService CreateDefault()
    {
        return CreateFor(_telegramBotClient);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _started = true;
        return Task.WhenAll(_cache.Values.Select(s => s.StartAsync(cancellationToken)));
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.WhenAll(_cache.Values.Select(s => s.StopAsync(cancellationToken)));
    }

    public void Dispose()
    {
        foreach (var (key, service) in _cache)
        {
            service.Dispose();
        }
        _cache.Clear();
    }
}


public class MassMessageSendingService : FixedActionsPerSecondsWorkerService<MassMessageSendingService.MessageRequest, Message>
{
    private static readonly List<MessageType> AllowedTypesToSend = new()
    {
        MessageType.Text,
        MessageType.Photo,
        MessageType.Video,
        MessageType.Document,
        MessageType.Animation
    };
    
    private TelegramBotClient _telegramBotClient;

    private readonly ConcurrentQueue<(MessageRequest request, TaskCompletionSource<Message> completionSource)> _messageQueue = new();

    public MassMessageSendingService() : base(25) { }

    public void InitFor(TelegramBotClient telegramBotClient, int targetApm = 25)
    {
        _telegramBotClient = telegramBotClient;
        base.ApsRate = targetApm;
    }

    protected override async Task OneAction(MessageRequest messageRequest, TaskCompletionSource<Message> resultCompletionSource)
    {
        Message msg = null;
        try
        {
            switch (messageRequest.MessageType)
            {
                case MessageType.Text:
                    msg = await _telegramBotClient.SendTextMessageAsync(
                        chatId: messageRequest.ChatId,
                        text: messageRequest.Text,
                        parseMode: ParseMode.Html,
                        disableWebPagePreview: true,
                        replyMarkup: messageRequest.ReplyMarkup);
                    break;
                case MessageType.Photo:
                    Debug.Assert(messageRequest.Photo != null, "message.Photo != null");
                    msg = await _telegramBotClient.SendPhotoAsync(
                        chatId: messageRequest.ChatId,
                        photo: new InputFileId(messageRequest.Photo.Last().FileId),
                        caption: messageRequest.Text,
                        parseMode: ParseMode.Html,
                        replyMarkup: messageRequest.ReplyMarkup);
                    break;
                case MessageType.Video:
                    Debug.Assert(messageRequest.Video != null, "message.Video != null");
                    msg = await _telegramBotClient.SendVideoAsync(
                        chatId: messageRequest.ChatId,
                        video: new InputFileId(messageRequest.Video.FileId),
                        duration: messageRequest.Video.Duration,
                        width: messageRequest.Video.Width,
                        height: messageRequest.Video.Height,
                        caption: messageRequest.Text,
                        parseMode: ParseMode.Html,
                        replyMarkup: messageRequest.ReplyMarkup
                    );
                    break;
                case MessageType.Document:
                    Debug.Assert(messageRequest.Document != null, "message.Document != null");
                    msg = await _telegramBotClient.SendDocumentAsync(
                        chatId: messageRequest.ChatId,
                        document: new InputFileId(messageRequest.Document.FileId),
                        caption: messageRequest.Text,
                        parseMode: ParseMode.Html,
                        replyMarkup: messageRequest.ReplyMarkup
                    );
                    break;
                case MessageType.Animation:
                    Debug.Assert(messageRequest.Animation != null, "message.Animation != null");
                    msg = await _telegramBotClient.SendAnimationAsync(
                        chatId: messageRequest.ChatId,
                        animation: new InputFileId(messageRequest.Animation.FileId),
                        caption: messageRequest.Text,
                        parseMode: ParseMode.Html,
                        replyMarkup: messageRequest.ReplyMarkup
                    );
                    break;
                default:
                    resultCompletionSource.SetException(new Exception(
                        $"Не можу відправити повідомлення в чат {messageRequest.ChatId} тому що для масової відправки повідомлення типу {messageRequest.MessageType} не підтримуються."));
                    break;
            }

            resultCompletionSource.SetResult(msg);
        }
        catch (Exception e)
        {
            resultCompletionSource.SetException(e);
            Console.WriteLine(e);
        }
    }

    public override Task<Message> EnqueueMessage(MessageRequest messageRequest)
    {
        if (!AllowedTypesToSend.Contains(messageRequest.MessageType))
            throw new Exception(
                $"Не можу запланувати відправку повідомлення в чат {messageRequest.ChatId} тому що для масової відправки повідомлення типу {messageRequest.MessageType} не підтримуються.");
        return base.EnqueueMessage(messageRequest);
    }

    [Serializable]
    public class MessageRequest
    {
        public long ChatId { get; set; }
        public string Text { get; set; }
        public MessageType MessageType { get; set; }
        public IReplyMarkup? ReplyMarkup { get; set; }
        public PhotoSize[]? Photo { get; set; }
        public Video? Video { get; set; }
        public Document? Document { get; set; }
        public Animation? Animation { get; set; }

        public MessageRequest()
        {
        }

        public MessageRequest(Message source, long chatId)
        {
            MessageType = source.Type;
            ChatId = chatId;
            Text = source.GetHTML();
            ReplyMarkup = source.ReplyMarkup;
            
            switch (source.Type)
            {
                case MessageType.Text:
                    break;
                
                case MessageType.Photo:
                    Debug.Assert(source.Photo != null, "message.Photo != null");
                    Photo = source.Photo;
                    break;
                
                case MessageType.Video:
                    Debug.Assert(source.Video != null, "message.Video != null");
                    Video = source.Video;
                    break;
                
                case MessageType.Document:
                    Debug.Assert(source.Document != null, "message.Document != null");
                    Document = source.Document;
                    break;
                
                case MessageType.Animation:
                    Debug.Assert(source.Animation != null, "message.Animation != null");
                    this.Animation = source.Animation;
                    break;
                    
                
                default: throw new Exception($"Unknown message type for repost: {source.Type}");
            }
        }

        public MessageRequest(string htmlMessage, long chatId)
        {
            MessageType = MessageType.Text;
            Text = htmlMessage;
            ChatId = chatId;
        }
    }
}