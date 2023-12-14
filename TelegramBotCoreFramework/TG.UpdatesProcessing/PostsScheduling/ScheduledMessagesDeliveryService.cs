using Helpers.PredefinedChannels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TG.UpdatesProcessing.PostsScheduling;

namespace TG.UpdatesProcessing;

public class ScheduledMessagesDeliveryService : IHostedService
{
    private readonly ILogger<TgIngestionProcessingService> _logger;
    private readonly LoggingChannel _loggingChannel;
    private readonly ScheduledMessagesSettings _scheduledMessagesSettings;
    private readonly ScheduledMessagesPublisherHelper _scheduledMessagesPublisherHelper;
    private Task? _task = null;

    public ScheduledMessagesDeliveryService(
        ILogger<TgIngestionProcessingService> logger,
        LoggingChannel loggingChannel,
        ScheduledMessagesSettings scheduledMessagesSettings,
        ScheduledMessagesPublisherHelper scheduledMessagesPublisherHelper)
    {
        _logger = logger;
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
                _logger.LogError("An error occurred while executing CheckScheduledMessagesAndSend", e);
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
                    var sent = await _scheduledMessagesPublisherHelper.SendMessage(message);
                    if (message.PublishLifetimeMinutes > 0)
                    {
                        await _scheduledMessagesPublisherHelper.MarkWaitingToRemovalMessage(message, sent);
                        await _scheduledMessagesPublisherHelper.NotifyAdminsMessageSent(message);
                    }
                    else
                    {
                        await _scheduledMessagesPublisherHelper.ArchiveMessage(message, sent);
                        await _scheduledMessagesPublisherHelper.NotifyAdminsMessageSent(message);
                    }
                }
                catch (Exception e)
                {
                    message.AllowedToSend = false;
                    await _scheduledMessagesSettings.UpdateMessage(message);

                    _logger.LogInformation("scheduled message processing", e);
                    await _loggingChannel.LogExceptionToServiceChannel($"Error while trying to post scheduled message {message.Index}", e);
                }
            }
        }

        if (messagesToRemove.Any())
        {
            foreach (var message in messagesToRemove)
            {
                try
                {
                    await _scheduledMessagesPublisherHelper.NotifyAdminsMessageRemovedAndUpdateViews(message);
                    await _scheduledMessagesPublisherHelper.ArchiveMessage(message);
                }
                catch (Exception e)
                {
                    message.AllowedToSend = false;
                    await _scheduledMessagesSettings.UpdateMessage(message);

                    _logger.LogInformation("scheduled message processing", e);
                    await _loggingChannel.LogExceptionToServiceChannel($"Error while trying to remove scheduled message {message.Index}", e);
                }
            }
        }
    }
}