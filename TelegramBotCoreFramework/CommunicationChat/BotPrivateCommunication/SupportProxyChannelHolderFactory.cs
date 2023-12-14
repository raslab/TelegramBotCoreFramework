using Analytics.UsersDatabase;
using Helpers.PredefinedChannels;
using Telegram.Bot;

namespace CommunicationChat.BotPrivateCommunication;

public class SupportProxyChannelHolderFactory
{
    private readonly ProjectTeamCommunication _projectTeamCommunication;
    private readonly TelegramBotClient _botClient;
    private readonly SubscribersDatabase _subscribersDatabase;
    private Dictionary<string, SupportProxyChannelHolder> _holders = new Dictionary<string, SupportProxyChannelHolder>();

    public SupportProxyChannelHolderFactory(
        ProjectTeamCommunication projectTeamCommunication,
        TelegramBotClient botClient, SubscribersDatabase subscribersDatabase)
    {
        _projectTeamCommunication = projectTeamCommunication;
        _botClient = botClient;
        _subscribersDatabase = subscribersDatabase;
    }

    public SupportProxyChannelHolder Create(long? supportChatId, TelegramBotClient botClient, IProxyChannelSubscribersRepository subscribersRepository)
    {
        var key = supportChatId + botClient.BotId.ToString();
        if (_holders.ContainsKey(key))
        {
            return _holders[key];
        }
        var holder = new SupportProxyChannelHolder(_projectTeamCommunication);
        holder.InitiateFor(supportChatId, botClient, subscribersRepository);
        _holders.Add(key, holder);
        return holder;
    }
    
    public SupportProxyChannelHolder CreateDefault(long? supportChatId)
    {
        return Create(supportChatId, _botClient, _subscribersDatabase);
    }

    public void ClearCache()
    {
        _holders.Clear();
    }
}