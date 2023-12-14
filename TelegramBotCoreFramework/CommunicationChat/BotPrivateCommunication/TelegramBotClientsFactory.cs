using Helpers;
using Telegram.Bot;

namespace CommunicationChat.BotPrivateCommunication;

public class TelegramBotClientsFactory
{
    private Dictionary<string, TelegramBotClient> _cache = new Dictionary<string, TelegramBotClient>();
    
    public TelegramBotClient GetClientFor(string accessToken)
    {
        if (_cache.TryGetValue(accessToken, out var client))
            return client;

        client = new TelegramBotClient(accessToken);
        _cache.Add(accessToken, client);
        return client;
    }
    
    public TelegramBotClient GetDefault()
    {
        return GetClientFor(Env.TelegramBotToken!);
    }
}