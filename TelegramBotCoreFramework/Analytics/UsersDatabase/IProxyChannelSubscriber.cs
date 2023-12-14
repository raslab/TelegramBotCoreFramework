namespace Analytics.UsersDatabase;

public interface IProxyChannelSubscriber
{
    long Id { get; }
    int CommunicationChatThreadId { get; set; }
    string FirstName { get; }
    string LastName { get; }
    string UserName { get; }
    bool IsBotBlockedByUser { get; set; }
    public List<MessageDetail>? MessagesHistory { get; set; }
    CaptchaStatus CaptchaStatus { get; set; }
}