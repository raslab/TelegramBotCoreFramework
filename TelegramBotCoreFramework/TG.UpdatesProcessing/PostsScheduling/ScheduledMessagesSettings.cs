using Google.Cloud.Firestore;
using Helpers;
using Helpers.Extensions;
using Newtonsoft.Json;
using Telegram.Bot.Types;

namespace TG.UpdatesProcessing.PostsScheduling;

public class ScheduledMessagesSettings : ScheduledMessagesSettingsBase<ScheduledMessage>
{
    public ScheduledMessagesSettings(FirestoreDb firestoreDb, ConfigurationStorage configurationStorage) 
        : base(firestoreDb, configurationStorage, "ScheduledMessages")
    {
    }
    
    internal async Task<ScheduledMessage> AddMessage(long? creatorId, Message? message)
    {
        var m = new ScheduledMessage{
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

public class ScheduledMessagesArchive : ScheduledMessagesSettingsBase<ScheduledMessageArchive>
{
    public ScheduledMessagesArchive(FirestoreDb firestoreDb, ConfigurationStorage configurationStorage) 
        : base(firestoreDb, configurationStorage, "ScheduledMessagesArchive")
    {
    }

    public Task AddMessage(ScheduledMessageArchive archiveMessage)
    {
        return ScheduledMessagesRepository.UpdateAsync(archiveMessage.Index.ToString(), archiveMessage);
    }
}

public abstract class ScheduledMessagesSettingsBase<T>
    where T:ScheduledMessage
{
    protected readonly ConfigurationStorage ConfigurationStorage;
    protected readonly FirestoreRepository<T> ScheduledMessagesRepository;

    public ScheduledMessagesSettingsBase(FirestoreDb firestoreDb, ConfigurationStorage configurationStorage, string collectionName)
    {
        ConfigurationStorage = configurationStorage;
        ScheduledMessagesRepository = new FirestoreRepository<T>(firestoreDb, collectionName);
    }

    public async Task<T[]> GetMessagesReadyToSend()
    {
        var allowed =  await ScheduledMessagesRepository.Collection
            .WhereEqualTo(nameof(ScheduledMessage.State), ScheduledMessageState.Preparing)
            .WhereEqualTo(nameof(ScheduledMessage.AllowedToSend), true)
            .GetSnapshotAsync();
        var mm =  allowed.Documents.Select(d => d.ConvertTo<T>())
            .Where(m=> m.PublishDate.ToDateTime() <= DateTime.UtcNow)
            .ToArray();
        return mm;
    }

    public async Task<T[]> GetMessagesReadyToRemove()
    {
        var snapshots = await ScheduledMessagesRepository.Collection
            .WhereEqualTo(nameof(ScheduledMessage.State), ScheduledMessageState.WaitingForRemoval)
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


public enum ScheduledMessageState
{
    Preparing,
    WaitingForRemoval,
    Removed
}

[FirestoreData, Serializable]
public class ScheduledMessage
{
    [FirestoreProperty] public long Index { get; set; }
    [FirestoreProperty] public Google.Cloud.Firestore.Timestamp CreateDate { get; set; }
    [FirestoreProperty] public long? Creator { get; set; }
    [FirestoreProperty] public bool AllowedToSend { get; set; }
    [FirestoreProperty] public Google.Cloud.Firestore.Timestamp PublishDate { get; set; }
    [FirestoreProperty] public int PublishLifetimeMinutes { get; set; }
    [FirestoreProperty] public List<long>? ChatIdsToSend { get; set; }
    [FirestoreProperty] public ScheduledMessageState State { get; set; } = ScheduledMessageState.Preparing;
    [FirestoreProperty] public PublishedMessageInfo[]? SentMessages { get; set; }

    public Message? Message { get; set; }

    [FirestoreProperty]
    public string MessageJson
    {
        get => JsonConvert.SerializeObject(Message);
        set => Message = JsonConvert.DeserializeObject<Message>(value);
    }
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
public class ScheduledMessageArchive: ScheduledMessage
{
    public ScheduledMessageArchive()
    {
    }

    public ScheduledMessageArchive(ScheduledMessage source)
    {
        Index = source.Index;
        CreateDate = source.CreateDate;
        Creator = source.Creator;
        AllowedToSend = source.AllowedToSend;
        PublishDate = source.PublishDate;
        PublishLifetimeMinutes = source.PublishLifetimeMinutes;
        ChatIdsToSend = source.ChatIdsToSend;
        State = source.State;
        SentMessages = source.SentMessages;
        Message = source.Message;
    }

    [FirestoreProperty] public Google.Cloud.Firestore.Timestamp ArchiveTime { get; set; }
}