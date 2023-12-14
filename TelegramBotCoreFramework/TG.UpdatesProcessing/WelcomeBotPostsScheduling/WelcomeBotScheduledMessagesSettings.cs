using Google.Cloud.Firestore;
using Helpers;
using Helpers.Extensions;
using Newtonsoft.Json;
using Telegram.Bot.Types;

namespace TG.UpdatesProcessing.WelcomeBotPostsScheduling;

public class WelcomeBotScheduledMessagesSettings : WelcomeBotScheduledMessagesSettingsBase<WelcomeBotScheduledMessage>
{
    public WelcomeBotScheduledMessagesSettings(FirestoreDb firestoreDb, ConfigurationStorage configurationStorage) 
        : base(firestoreDb, configurationStorage, "WelcomeBotScheduledMessages")
    {
    }
    
    internal async Task<WelcomeBotScheduledMessage> AddMessage(long? creatorId, Message? message)
    {
        var m = new WelcomeBotScheduledMessage{
            Index = await ConfigurationStorage.GetAndIncIndexer(this.GetType().Name),
            Message = message,
            CreateDate = DateTime.UtcNow.ToFirestoreTimestamp(),
            Creator = creatorId,
            AllowedToSend = false
        };
        await UpdateMessage(m);
        return m;
    }
}

public class WelcomeBotScheduledMessagesArchive : WelcomeBotScheduledMessagesSettingsBase<WelcomeBotScheduledMessageArchive>
{
    public WelcomeBotScheduledMessagesArchive(FirestoreDb firestoreDb, ConfigurationStorage configurationStorage) 
        : base(firestoreDb, configurationStorage, "WelcomeBotScheduledMessagesArchive")
    {
    }

    public Task AddMessage(WelcomeBotScheduledMessageArchive archiveMessage)
    {
        return ScheduledMessagesRepository.UpdateAsync(archiveMessage.Index.ToString(), archiveMessage);
    }
}

public abstract class WelcomeBotScheduledMessagesSettingsBase<T>
    where T:WelcomeBotScheduledMessage
{
    protected readonly ConfigurationStorage ConfigurationStorage;
    protected readonly FirestoreRepository<T> ScheduledMessagesRepository;

    public WelcomeBotScheduledMessagesSettingsBase(FirestoreDb firestoreDb, ConfigurationStorage configurationStorage, string collectionName)
    {
        ConfigurationStorage = configurationStorage;
        ScheduledMessagesRepository = new FirestoreRepository<T>(firestoreDb, collectionName);
    }

    public async Task<T[]> GetMessagesReadyToSend()
    {
        
        var allowed =  await ScheduledMessagesRepository.Collection
            .WhereEqualTo(nameof(WelcomeBotScheduledMessage.State), WelcomeBotScheduledMessageState.Preparing)
            .WhereEqualTo(nameof(WelcomeBotScheduledMessage.AllowedToSend), true)
            .GetSnapshotAsync();
        var mm =  allowed.Documents.Select(d => d.ConvertTo<T>())
            .Where(m=> m.PublishDate.ToDateTime() <= DateTime.UtcNow)
            .ToArray();
        return mm;
    }

    public async Task<T[]> GetMessagesReadyToRemove()
    {
        var snapshots = await ScheduledMessagesRepository.Collection
            .WhereEqualTo(nameof(WelcomeBotScheduledMessage.State), WelcomeBotScheduledMessageState.WaitingForRemoval)
            .GetSnapshotAsync();
        return snapshots.Documents.Select(d => d.ConvertTo<T>())
            .Where(m => m.PublishDate.ToDateTime().AddMinutes(m.PublishLifetimeMinutes) <= DateTime.UtcNow).ToArray();
    }

    public async Task<T[]> GetAllScheduledMessages()
    {
        return (await ScheduledMessagesRepository.GetAllAsync()).Select(p => p.Item2).ToArray();
    }

    public Task<T> GetMessage(string index)
    {
        return ScheduledMessagesRepository.GetAsync(index);
    }

    public Task RemoveMessage(string mIndex)
    {
        return ScheduledMessagesRepository.DeleteAsync(mIndex);
    }

    public Task UpdateMessage(T message)
    {
        return ScheduledMessagesRepository.UpdateAsync(message.Index.ToString(), message);
    }
}


public enum WelcomeBotScheduledMessageState
{
    Preparing = 0,
    Delivering = 1,
    WaitingForRemoval = 2,
    Cleaning = 3,
    Removed = 4
}

[FirestoreData, Serializable]
public class WelcomeBotScheduledMessage
{
    [FirestoreProperty] public long Index { get; set; }
    [FirestoreProperty] public Google.Cloud.Firestore.Timestamp CreateDate { get; set; }
    [FirestoreProperty] public long? Creator { get; set; }
    [FirestoreProperty] public bool AllowedToSend { get; set; }
    [FirestoreProperty] public Google.Cloud.Firestore.Timestamp PublishDate { get; set; }
    [FirestoreProperty] public int PublishLifetimeMinutes { get; set; }
    [FirestoreProperty] public int TargetDeliveryAmount { get; set; }
    [FirestoreProperty] public WelcomeBotScheduledMessageState State { get; set; } = WelcomeBotScheduledMessageState.Preparing;

    public Message? Message { get; set; }

    [FirestoreProperty]
    public string MessageJson
    {
        get => JsonConvert.SerializeObject(Message);
        set => Message = JsonConvert.DeserializeObject<Message>(value);
    }

    [FirestoreProperty] public DeliveryWelcomeBotMessageReport? DeliveryReport { get; set; }
    [FirestoreProperty] public CleanupWelcomeBotMessageReport? CleanupReport { get; set; }
}

[FirestoreData, Serializable]
public class PublishedMessageInfo
{
    [FirestoreProperty] public long ChatId {get; set;}
    [FirestoreProperty] public int MessageId {get; set; }
    [FirestoreProperty] public int Views {get; set;}
    [FirestoreProperty] public int Reactions {get; set;}
}

[FirestoreData, Serializable]
public class WelcomeBotScheduledMessageArchive: WelcomeBotScheduledMessage
{
    public WelcomeBotScheduledMessageArchive()
    {
    }

    public WelcomeBotScheduledMessageArchive(WelcomeBotScheduledMessage source)
    {
        Index = source.Index;
        CreateDate = source.CreateDate;
        Creator = source.Creator;
        AllowedToSend = source.AllowedToSend;
        PublishDate = source.PublishDate;
        PublishLifetimeMinutes = source.PublishLifetimeMinutes;
        TargetDeliveryAmount = source.TargetDeliveryAmount;
        State = source.State;
        Message = source.Message;
        DeliveryReport = source.DeliveryReport;
        CleanupReport = source.CleanupReport;
    }

    [FirestoreProperty] public Google.Cloud.Firestore.Timestamp ArchiveTime { get; set; }
}

[FirestoreData]
public class DeliveryWelcomeBotMessageReport
{
    [FirestoreProperty] public Timestamp StartTime { get; set; }
    [FirestoreProperty] public int DeliveredMessages { get; set; }
    [FirestoreProperty] public int BlockedByUser { get; set; }
    [FirestoreProperty] public Timestamp EndTime { get; set; }
}

[FirestoreData]
public class CleanupWelcomeBotMessageReport
{
    [FirestoreProperty] public Timestamp StartTime { get; set; }
    [FirestoreProperty] public int CleanedMessages { get; set; }
    [FirestoreProperty] public int Errors { get; set; }
    [FirestoreProperty] public Timestamp EndTime { get; set; }
}