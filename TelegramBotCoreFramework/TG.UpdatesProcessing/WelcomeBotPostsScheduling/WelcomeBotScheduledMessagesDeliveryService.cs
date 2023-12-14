using Helpers.PredefinedChannels;
using Microsoft.Extensions.Hosting;

namespace TG.UpdatesProcessing.WelcomeBotPostsScheduling;

public class WelcomeBotScheduledMessagesDeliveryService : IHostedService
{
    private readonly LoggingChannel _loggingChannel;
    private readonly WelcomeBotScheduledMessagesSettings _scheduledMessagesSettings;
    private readonly WelcomeBotScheduledMessagesPublisherHelper _scheduledMessagesPublisherHelper;
    private Task? _task = null;

    public WelcomeBotScheduledMessagesDeliveryService(
        LoggingChannel loggingChannel,
        WelcomeBotScheduledMessagesSettings scheduledMessagesSettings,
        WelcomeBotScheduledMessagesPublisherHelper scheduledMessagesPublisherHelper)
    {
        _loggingChannel = loggingChannel;
        _scheduledMessagesSettings = scheduledMessagesSettings;
        _scheduledMessagesPublisherHelper = scheduledMessagesPublisherHelper;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _task = UpdatePostsScheduleAsync(cancellationToken);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return _task!;
    }

    public async Task UpdatePostsScheduleAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await CheckScheduledMessagesAndSend();
            }
            catch(Exception e)
            {
                await _loggingChannel.LogExceptionToServiceChannel("An error occurred while executing CheckScheduledMessagesAndSend", e);
            }
            await Task.Delay(TimeSpan.FromMinutes(1), token);
        }
    }
    
    private async Task CheckScheduledMessagesAndSend()
    {
        var messagesToSend = await _scheduledMessagesSettings.GetMessagesReadyToSend();
        var messagesToRemove = await _scheduledMessagesSettings.GetMessagesReadyToRemove();

        if (messagesToSend.Any())
        {
            foreach (var message in messagesToSend)
            {
                try
                {
                    await _scheduledMessagesPublisherHelper.NotifyAdminsMessageStartSending(message);
                    var report = await _scheduledMessagesPublisherHelper.SendMessage(message);
                    if (message.PublishLifetimeMinutes > 0)
                    {
                        await _scheduledMessagesPublisherHelper.MarkWaitingToRemovalMessage(message, report);
                        await _scheduledMessagesPublisherHelper.NotifyAdminsMessageSent(message, report);
                    }
                    else
                    {
                        await _scheduledMessagesPublisherHelper.ArchiveMessage(message, report);
                        await _scheduledMessagesPublisherHelper.NotifyAdminsMessageSent(message, report);
                    }
                }
                catch (Exception e)
                {
                    message.AllowedToSend = false;
                    await _scheduledMessagesSettings.UpdateMessage(message);
                    await _loggingChannel.LogExceptionToServiceChannel($"Error while trying to post scheduled delivery {message.Index}", e);
                }
            }
        }

        if (messagesToRemove.Any())
        {
            foreach (var message in messagesToRemove)
            {
                try
                {
                    var report = await _scheduledMessagesPublisherHelper.CleanupMessage(message);
                    
                    await _scheduledMessagesPublisherHelper.NotifyAdminsMessageCleanup(message, report);
                    await _scheduledMessagesPublisherHelper.ArchiveMessage(message, report);
                }
                catch (Exception e)
                {
                    message.AllowedToSend = false;
                    await _scheduledMessagesSettings.UpdateMessage(message);
                    await _loggingChannel.LogExceptionToServiceChannel($"Error while trying to remove scheduled delivery {message.Index}", e);
                }
            }
        }
    }
}