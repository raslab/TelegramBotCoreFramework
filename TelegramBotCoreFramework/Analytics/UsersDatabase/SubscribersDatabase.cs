using Google.Cloud.Firestore;
using Helpers;
using Helpers.Extensions;
using Telegram.Bot.Types;

namespace Analytics.UsersDatabase;

public class SubscribersDatabase: IProxyChannelSubscribersRepository
{
    protected string SubscribersCollectionName { get; set; } = "SubscribersDatabase";

    protected FirestoreRepository<SubscriberDto> SubscribersRepository;

    private SubscribersDatabaseStats? _stats = null;

    public SubscribersDatabase(FirestoreDb firestoreDb)
    {
        SubscribersRepository = new FirestoreRepository<SubscriberDto>(firestoreDb, SubscribersCollectionName);
    }

    public Task<SubscriberDto> GetSubscriber(long subId)
    {
        return SubscribersRepository.GetAsync(subId.ToString());
    }

    Task<IProxyChannelSubscriber?> IProxyChannelSubscribersRepository.RegisterFromCommunication(User user)
    {
        return RegisterFromCommunication(user).ContinueWith(t => (IProxyChannelSubscriber?)t.Result);
    }

    Task IProxyChannelSubscribersRepository.UpdateSubscriber(IProxyChannelSubscriber sub)
    {
        return UpdateSubscriber((SubscriberDto)sub);
    }

    public Task UpdateSubscriber(SubscriberDto sub)
    {
        return SubscribersRepository.UpdateAsync(sub.Id.ToString(), sub);
    }

    public async Task<long?> PendingRequestsCount(long channelId)
    {
        return (await SubscribersRepository.Collection
            .WhereArrayContains(nameof(SubscriberDto.PendingRequestToChannels),channelId)
            .Count()
            .GetSnapshotAsync()).Count;
    }

    public async Task<SubscriberDto[]> GetPendingRequestUsers(long channelId, int limit)
    {
        var snap = await SubscribersRepository.Collection
            .WhereArrayContains(nameof(SubscriberDto.PendingRequestToChannels), channelId)
            .Limit(limit)
            .GetSnapshotAsync();
        return snap.Documents.Select(doc => doc.ConvertTo<SubscriberDto>()).ToArray();
    }

    public async Task<long?> GetAvailableToSendCount()
    {
        return (await SubscribersRepository.Collection
            .WhereEqualTo(nameof(SubscriberDto.IsBotBlockedByUser), false)
            .WhereIn(nameof(SubscriberDto.CaptchaStatus), new [] {CaptchaStatus.Passed, CaptchaStatus.Unknown})
            .Count()
            .GetSnapshotAsync()).Count;
    }
    
    
    public CollectionReference GetDbCollection()
    {
        return SubscribersRepository.Collection;
    }

    public Query GetAvailableToSendSubsQuery()
    {
        return SubscribersRepository.Collection
            .WhereEqualTo(nameof(SubscriberDto.IsBotBlockedByUser), false)
            .WhereIn(nameof(SubscriberDto.CaptchaStatus), new[] { CaptchaStatus.Passed, CaptchaStatus.Unknown })
            .OrderBy(nameof(SubscriberDto.RegistrationDate));
    }

    public Query GetSubsWithPlacedMessageIdQuery(long messageIndex)
    {
        return SubscribersRepository.Collection
            .WhereArrayContains(nameof(SubscriberDto.PlacedNowMessages), messageIndex);
    }

    public async Task<SubscribersDatabaseStats> GetStats()
    {
        if (_stats != null && _stats.LastUpdating > DateTime.UtcNow.AddHours(-24))
            return _stats;
        _stats = new SubscribersDatabaseStats()
        {
            LastUpdating = DateTime.UtcNow,
            AllSubscribersInDb = (await SubscribersRepository.Collection.Count().GetSnapshotAsync()).Count,
            SubsInWelcomeBot = (await SubscribersRepository.Collection
                .WhereNotEqualTo(nameof(SubscriberDto.CaptchaStatus), CaptchaStatus.None)
                .Count().GetSnapshotAsync()).Count,
            ActiveUsers = (await SubscribersRepository.Collection
                .WhereEqualTo(nameof(SubscriberDto.CaptchaStatus), CaptchaStatus.Passed)
                .WhereEqualTo(nameof(SubscriberDto.IsBotBlockedByUser), false)
                .Count().GetSnapshotAsync()).Count,
            CaptchaSent = (await SubscribersRepository.Collection
                .WhereIn(nameof(SubscriberDto.CaptchaStatus), new [] {CaptchaStatus.Sent, CaptchaStatus.Passed, CaptchaStatus.Ignored})
                .WhereEqualTo(nameof(SubscriberDto.IsBotBlockedByUser), false)
                .Count().GetSnapshotAsync()).Count,
            CaptchaPassedTotal = (await SubscribersRepository.Collection
                .WhereEqualTo(nameof(SubscriberDto.CaptchaStatus), CaptchaStatus.Passed)
                .Count().GetSnapshotAsync()).Count,
            NotReceivedAds = (await SubscribersRepository.Collection
                .WhereEqualTo(nameof(SubscriberDto.DeliveredAdMessagesCount), 0)
                .WhereEqualTo(nameof(SubscriberDto.CaptchaStatus), CaptchaStatus.Passed)
                .WhereEqualTo(nameof(SubscriberDto.IsBotBlockedByUser), false)
                .Count().GetSnapshotAsync()).Count,
            BlockedBot = (await SubscribersRepository.Collection
                .WhereEqualTo(nameof(SubscriberDto.IsBotBlockedByUser), true)
                .Count().GetSnapshotAsync()).Count,
        };
        _stats.CaptchaConversion = _stats.CaptchaPassedTotal * 1f / _stats.SubsInWelcomeBot;
        return _stats;
    }

    Task<IProxyChannelSubscriber?> IProxyChannelSubscribersRepository.GetSubscriber(long userId)
    {
        return GetSubscriber(userId).ContinueWith(t => (IProxyChannelSubscriber?)t.Result);
    }

    public async Task<SubscriberDto?> RegisterFromCommunication(User user)
    {
        var sub = await GetSubscriber(user.Id);
        if (sub != null)
            return sub;
        sub = new SubscriberDto();
        sub.Id = user.Id;
        if (!string.IsNullOrEmpty(user.FirstName)) sub.FirstName = user.FirstName;
        if (!string.IsNullOrEmpty(user.LastName)) sub.LastName = user.LastName;
        if (!string.IsNullOrEmpty(user.Username)) sub.UserName = user.Username;
        if (!string.IsNullOrEmpty(user.LanguageCode)) sub.Language = user.LanguageCode;
        sub.RegistrationSource = SubscriberCameFrom.DirectCommunication;
        sub.RegistrationDate = DateTime.UtcNow.ToFirestoreTimestamp();
        await UpdateSubscriber(sub);
        return sub;
    }

    public async Task<SubscriberDto?> GetSubscriberForCommunicationChannel(int messageMessageThreadId)
    {
        var sub = await SubscribersRepository.Collection.WhereEqualTo(nameof(SubscriberDto.CommunicationChatThreadId),
            messageMessageThreadId).GetSnapshotAsync();
        return sub.Documents.Select(doc => doc.ConvertTo<SubscriberDto>()).FirstOrDefault();
    }

    Task<IProxyChannelSubscriber> IProxyChannelSubscribersRepository.GetSubscriberForCommunicationChannel(int messageMessageThreadId)
    {
        return GetSubscriberForCommunicationChannel(messageMessageThreadId).ContinueWith(t => (IProxyChannelSubscriber)t.Result);
    }
}