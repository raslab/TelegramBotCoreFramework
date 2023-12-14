using Analytics.HistoricalData;
using Google.Cloud.PubSub.V1;
using Helpers;
using Helpers.PredefinedChannels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TG.UpdatesProcessing;

public class CollectAnalyticsService: IHostedService
{
    private readonly ILogger<CollectAnalyticsService> _logger;
    private readonly LoggingChannel _loggingChannel;
    private readonly ChannelsAnalyticsCollecting _channelsAnalyticsCollecting;
    private Task _task;

    public CollectAnalyticsService(
        ILogger<CollectAnalyticsService> logger,
        LoggingChannel loggingChannel,
        ChannelsAnalyticsCollecting channelsAnalyticsCollecting)
    {
        _logger = logger;
        _loggingChannel = loggingChannel;
        _channelsAnalyticsCollecting = channelsAnalyticsCollecting;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _task = StreamProcessAsync(cancellationToken);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return _task;
    }
    
    public async Task StreamProcessAsync(CancellationToken token)
    {
        var subscription = await SubscriberClient.CreateAsync(new SubscriptionName(Env.GoogleProjectName, Env.AnalyticsScheduleSubscriptionName));
        await subscription.StartAsync(async (msg, cancellationToken) =>
        {
            var message = msg.Data.ToStringUtf8();
            try
            {
                _logger.LogInformation($"Analytics triggered message: " + message);

                switch (message)
                {
                    case "collect_channel_admin_log":
                        // cron: */20 * * * *
                        await _channelsAnalyticsCollecting.CollectAndStoreChannelsAdminLog();
                        break;
                    
                    case "collect_last_messages_views":
                        // cron: */20 * * * *
                        await _channelsAnalyticsCollecting.CollectAndStoreLastMessagesViewsAnalytics();
                        break;

                    case "collect_channel_participants":
                        // cron: 0 3 * * *
                        await _channelsAnalyticsCollecting.CollectAndStoreChannelsParticipantsAnalytics();
                        break;

                    case "collect_channels_subscribers_count":
                        // cron: */20 * * * *
                        await _channelsAnalyticsCollecting.CollectAndStoreChannelsSubscribersCountAnalytics();
                        break;

                }
                
                // Return Reply.Ack to indicate this message has been handled.
                return SubscriberClient.Reply.Ack;
            }
            catch (Exception e)
            {
                _logger.LogError(e.ToString());
                await _loggingChannel.LogExceptionToServiceChannel($"Помилка під час збору аналітики типу <code>{message}</code>. Команда буде проігнорована.\n", e);
                return SubscriberClient.Reply.Ack;
            }
        });
    }
}