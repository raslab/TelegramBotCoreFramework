using Google.Cloud.Firestore;

namespace Analytics.UsersDatabase;

[FirestoreData]
public class SubscriberDto: IProxyChannelSubscriber
{
    [FirestoreProperty] public long Id { get; set; }
    [FirestoreProperty] public string? UserName { get; set; }
    [FirestoreProperty] public string? FirstName { get; set; }
    [FirestoreProperty] public string? LastName { get; set; }
    [FirestoreProperty] public string? Language { get; set; }
    [FirestoreProperty] public Timestamp RegistrationDate { get; set; }
    [FirestoreProperty] public SubscriberCameFrom RegistrationSource { get; set; } = SubscriberCameFrom.Unknown;
    
    // subscriptions info
    [FirestoreProperty] public List<long> PendingRequestToChannels { get; set; } = new List<long>();
    [FirestoreProperty] public List<long> JoinedInChannels { get; set; } = new List<long>();
    [FirestoreProperty] public List<string> CameFromDeepLinks { get; set; } = new List<string>();

    // interactions with bot
    [FirestoreProperty] public bool IsBotBlockedByUser { get; set; } = false;
    [FirestoreProperty] public CaptchaStatus CaptchaStatus { get; set; } = CaptchaStatus.None;
    [FirestoreProperty] public List<MessageDetail>? MessagesHistory { get; set; } = new List<MessageDetail>();
    [FirestoreProperty] public List<long> PlacedNowMessages { get; set; } = new List<long>();
    [FirestoreProperty] public Timestamp LastDelivery { get; set; }
    [FirestoreProperty] public int DeliveredAdMessagesCount { get; set; }
    [FirestoreProperty] public int CommunicationChatThreadId { get; set; } = 0;
}

public enum SubscriberCameFrom
{
    AnalyticsCollect = 0,
    InviteLink = 1,
    ChannelJoinRequest = 2,
    OrganicFromBot = 3,
    OrganicFromChannels = 4,
    Unknown = 5,
    DirectCommunication
}

[FirestoreData]
public class MessageDetail
{
    [FirestoreProperty] public int MessageId { get; set; }
    [FirestoreProperty] public MessageType MessageType { get; set; }
    [FirestoreProperty] public Timestamp SentTime { get; set; }
    [FirestoreProperty] public long ScheduledMessageId { get; set; }
}


public enum RequestStatus
{
    None = 0,
    Pending = 1,
    Approved = 2 
}

public enum CaptchaStatus
{
    None = 0,
    Sent = 1, 
    Passed = 2,
    Ignored = 3,
    Unknown = 4
}

public enum MessageType
{
    Captcha = 0, 
    Welcome = 1, 
    Advertisement = 2
}
