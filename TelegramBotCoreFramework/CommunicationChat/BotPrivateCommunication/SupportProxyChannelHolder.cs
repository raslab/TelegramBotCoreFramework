using Analytics.UsersDatabase;
using Helpers.PredefinedChannels;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace CommunicationChat.BotPrivateCommunication;

public class SupportProxyChannelHolder
{
    private readonly ProjectTeamCommunication _projectTeamCommunication;
    
    private TelegramBotClient _telegramBotClient;
    private long? _communicationChannelId;
    private IProxyChannelSubscribersRepository _subscribersRepository;

    public SupportProxyChannelHolder(
        ProjectTeamCommunication projectTeamCommunication)
    {
        _projectTeamCommunication = projectTeamCommunication;
    }

    public bool CommunicationChannelSettedUp => _communicationChannelId != null;

    public void InitiateFor(long? supportChatId, TelegramBotClient botClient, IProxyChannelSubscribersRepository subscribersRepository)
    {
        _communicationChannelId = supportChatId;
        _telegramBotClient = botClient;
        _subscribersRepository = subscribersRepository;
    }

    private async Task<int> GetSubscriberThread(User user, IProxyChannelSubscriber? sub)
    {
        if (sub == null)
            sub = await _subscribersRepository.GetSubscriber(user.Id);
        if (sub == null)
        {
            sub = await _subscribersRepository.RegisterFromCommunication(user);
        }
        if (sub.CommunicationChatThreadId == 0)
        {
            var threadName = $"{sub.FirstName} {sub.LastName} (@{sub.UserName}) id{sub.Id}";
            var topic = await _telegramBotClient.CreateForumTopicAsync(
                chatId: _communicationChannelId,
                name: threadName
            );
            await _telegramBotClient.SendTextMessageAsync(
                chatId: _communicationChannelId,
                messageThreadId: topic.MessageThreadId,
                text: $"Користувачем розпочато новий діалог.\n" +
                      $"Поточні дані в базі по користувачу:\n<pre>{JsonConvert.SerializeObject(sub, Formatting.Indented)}</pre>\n\n" +
                      $"Всі написані вами повідомленя в цей топік будуть перенаправлені користувачу.",
                parseMode:ParseMode.Html,
                disableWebPagePreview: true);
            sub.CommunicationChatThreadId = topic.MessageThreadId;
            await _subscribersRepository.UpdateSubscriber(sub);
        }
        return sub.CommunicationChatThreadId;
    }


    public async Task SendMessageToCommunicationChannel(string message, User user)
    {
        if (_communicationChannelId == null)
        {
            await _projectTeamCommunication.SendMessageToAllOwners(
                $"Була спроба написати в чат комунікації, але він не заданий. " +
                $"Задайте його в налаштуваннях щоб не пропусткати повідомлення від користувачів." +
                $"\nПовідомлення:<pre>{message}<pre>");
            return;
        }
        
        var threadId = await GetSubscriberThread(user, null);
        await _telegramBotClient.SendTextMessageAsync(
            chatId: _communicationChannelId,
            text: message,
            parseMode: ParseMode.Html,
            disableWebPagePreview: true,
            messageThreadId: threadId
        );
    }

    public async Task ForwardMessageToCommunicationChannel(Update update, IProxyChannelSubscriber sub)
    {
        if (_communicationChannelId == null)
        {
            await _projectTeamCommunication.SendMessageToAllOwners(
                $"Була спроба переслати повідомлення в чат комунікації, але він не заданий. " +
                $"Задайте його в налаштуваннях щоб не пропусткати пnовідомлення від користувачів." +
                $"\nПовідомлення буде переслане в цей чат. На це повідомлення бот ніяк не буде реагувати в майбутньому, якщо хочете зв'язатись із відправником - пишять відправнику на пряму.");
            await _projectTeamCommunication.ForwardMessageToAllOwners(update.Message.Chat.Id, update.Message.MessageId);
            return;
        }
        
        var threadId = await GetSubscriberThread(update.Message.From, sub);
        try
        {
            try
            {
                await _telegramBotClient.SendMessageToChannel(
                    message: update.Message,
                    chatId: _communicationChannelId,
                    messageThreadId: threadId
                );
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
        catch (Exception e)
        {
            if (e.Message.Contains("the message can't be forwarded"))
            {
                await _telegramBotClient.SendTextMessageAsync(
                    _communicationChannelId,
                    $"Бот побачив повідомлення що не може бути переслане. JSON: <pre>{JsonConvert.SerializeObject(update.Message)}</pre>",
                    parseMode: ParseMode.Html,
                    replyToMessageId: update.Message.MessageId,
                    messageThreadId: threadId
                );
            }
        }
    }
}