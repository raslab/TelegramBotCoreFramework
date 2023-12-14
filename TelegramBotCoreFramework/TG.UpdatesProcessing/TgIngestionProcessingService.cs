using Google.Cloud.PubSub.V1;
using Helpers;
using Helpers.PredefinedChannels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Telegram.Bot.Types;

namespace TG.UpdatesProcessing;

public class TgIngestionProcessingService: IHostedService
{
    private readonly ILogger<TgIngestionProcessingService> _logger;
    private readonly BotUpdateProcessing _botUpdateProcessing;
    private readonly LoggingChannel _loggingChannel;
    private Task _task;

    public TgIngestionProcessingService(
        ILogger<TgIngestionProcessingService> logger,
        BotUpdateProcessing botUpdateProcessing,
        LoggingChannel loggingChannel)
    {
        _logger = logger;
        _botUpdateProcessing = botUpdateProcessing;
        _loggingChannel = loggingChannel;
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
        var subscription = await SubscriberClient.CreateAsync(new SubscriptionName(Env.GoogleProjectName, Env.UpdatesIngestionSubscriptionName));
        await subscription.StartAsync(async (msg, cancellationToken) =>
        {
            var json = msg.Data.ToStringUtf8();
            try
            {
                _logger.LogInformation($"Ingested message: " + json);

                var update = JsonConvert.DeserializeObject<Update>(json);
                await _botUpdateProcessing.ProcessMessage(update);
                
                // Return Reply.Ack to indicate this message has been handled.
                return SubscriberClient.Reply.Ack;
            }
            catch (Exception e)
            {
                _logger.LogError(e.ToString());
                await _loggingChannel.LogExceptionToServiceChannel($"Помилка під час обробки запиту до бота. Команда буде проігнорована.\nЗапит:\n{json}", e);
                return SubscriberClient.Reply.Ack;
            }
        });
    }
}