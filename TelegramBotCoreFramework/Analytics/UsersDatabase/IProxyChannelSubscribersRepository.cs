using Telegram.Bot.Types;

namespace Analytics.UsersDatabase;

public interface IProxyChannelSubscribersRepository
{
    Task<IProxyChannelSubscriber?> GetSubscriber(long userId);
    Task<IProxyChannelSubscriber?> RegisterFromCommunication(User user);
    Task UpdateSubscriber(IProxyChannelSubscriber sub);
    Task<IProxyChannelSubscriber> GetSubscriberForCommunicationChannel(int messageMessageThreadId);
}