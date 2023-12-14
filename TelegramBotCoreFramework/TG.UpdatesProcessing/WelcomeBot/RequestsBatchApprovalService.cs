using Analytics.UsersDatabase;
using Google.Cloud.Firestore;
using Helpers;
using Helpers.Extensions;
using Helpers.PredefinedChannels;
using Microsoft.Extensions.Hosting;

namespace TG.UpdatesProcessing.WelcomeBot;

[FirestoreData]
public class JoinRequestsOperationJobDto
{
    [FirestoreProperty] public long Id { get; set; }
    [FirestoreProperty] public long ChannelId { get; set; }
    [FirestoreProperty] public int AcceptedForNow { get; set; }
    [FirestoreProperty] public Timestamp StartTime { get; set; }
}


public class RequestsBatchApprovalService : IHostedService
{
    private readonly ChannelJoinRequestsProcessor _channelJoinRequestsProcessor;
    private readonly ConfigurationStorage _configurationStorage;
    private readonly SubscribersDatabase _subscribersDatabase;
    private readonly LoggingChannel _loggingChannel;
    private readonly ChannelsSettings _channelsSettings;
    private readonly ProjectTeamCommunication _projectTeamCommunication;
    private readonly FirestoreRepository<JoinRequestsOperationJobDto> _jobsRepo;

    private List<Task> _currentJobs = new List<Task>();
    private CancellationTokenSource _terminationSource = new CancellationTokenSource();
    private List<JoinRequestsOperationJobDto> _jobsCache = new List<JoinRequestsOperationJobDto>();

    public RequestsBatchApprovalService(ChannelJoinRequestsProcessor channelJoinRequestsProcessor,
        FirestoreDb firestoreDb, ConfigurationStorage configurationStorage,
        SubscribersDatabase subscribersDatabase, LoggingChannel loggingChannel,
        ChannelsSettings channelsSettings, ProjectTeamCommunication projectTeamCommunication)
    {
        _channelJoinRequestsProcessor = channelJoinRequestsProcessor;
        _configurationStorage = configurationStorage;
        _subscribersDatabase = subscribersDatabase;
        _loggingChannel = loggingChannel;
        _channelsSettings = channelsSettings;
        _projectTeamCommunication = projectTeamCommunication;
        _jobsRepo = new FirestoreRepository<JoinRequestsOperationJobDto>(firestoreDb, "SubscriberAcceptingJobs");
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _jobsCache = (await _jobsRepo.GetAllAsync()).Select(p=>p.Item2).ToList();
        foreach (var dto in _jobsCache)
        {
            _currentJobs.Add(StartApprovesFor(dto, _terminationSource.Token));
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _terminationSource.Cancel();
        await Task.WhenAll(_currentJobs.Where(j=>!j.IsCompleted));
        foreach (var dto in _jobsCache)
        {
            await _jobsRepo.UpdateAsync(dto.Id.ToString(), dto);
        }
    }

    public async Task<JoinRequestsOperationJobDto> StartAllRequestsApprovalFromChannel(ChannelSettingsDto channel)
    {
        var dto = new JoinRequestsOperationJobDto()
        {
            Id = await _configurationStorage.GetAndIncIndexer("SubscribersApprovalJob"),
            ChannelId = channel.ChannelId,
            StartTime = DateTime.UtcNow.ToFirestoreTimestamp(),
            AcceptedForNow = 0
        };
        _jobsCache.Add(dto);
        await _jobsRepo.UpdateAsync(dto.Id.ToString(), dto);
        _currentJobs.Add(StartApprovesFor(dto, _terminationSource.Token));
        return dto;
    }

    private async Task StartApprovesFor(JoinRequestsOperationJobDto dto, CancellationToken cancellationToken)
    {
        var wasError = false;
        try
        {
            while (true)
            {
                var subsBatch = await _subscribersDatabase.GetPendingRequestUsers(dto.ChannelId, 50);
                if (subsBatch.Length == 0)
                {
                    break;                
                }

                var tasks = subsBatch.Select(sub =>
                    _channelJoinRequestsProcessor.ApproveUserJoinAndSaveSub(sub, dto.ChannelId)
                        .ContinueWith(t => dto.AcceptedForNow++, cancellationToken));
                await Task.WhenAll(tasks);
            }

        }
        catch (Exception e)
        {
            await _loggingChannel.LogExceptionToServiceChannel(
                $"Помилка під час процесу прийому заявок {dto.Id}. Наразі було прийнято {dto.AcceptedForNow} підписників із початку роботи в {dto.StartTime.ToDateTime().UtcToUaTime():U}. Помила не вплине на підписки, для того щоб продовжити - спробуйте виправити помилку і запустити процес заново",
                e);
            wasError = true;
        }

        await _channelsSettings.LoadSchedule();
        var channel = _channelsSettings.ChannelSettings.FirstOrDefault(c => c.ChannelId == dto.ChannelId);
        var report = $"Процесс №{dto.Id} з прийому заявок завершено!\n" +
                     $"<b>Статус:</b> {(wasError ? "Сталась помилка, прийнята тільки частина заявок" : "Успішно")}\n" +
                     $"<b>Канал:</b> {channel.GetHtmlUrl()}\n" +
                     $"<b>Прийнято заявок:</b> {dto.AcceptedForNow}\n" +
                     $"<b>Початок роботи:</b> {dto.StartTime.ToDateTime().UtcToUaTime():U}\n" +
                     $"<b>Кінець роботи:</b> {DateTime.UtcNow.UtcToUaTime():U}";

        await _projectTeamCommunication.SendMessageToAllOwners(report);
        _jobsCache.Remove(dto);
        await _jobsRepo.DeleteAsync(dto.Id.ToString());
    }

    public List<JoinRequestsOperationJobDto> GetCurrentApprovalsJobs()
    {
        return _jobsCache;
    }
}