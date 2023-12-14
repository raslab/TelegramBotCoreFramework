using Google.Cloud.PubSub.V1;
using Helpers;

namespace TG.Webhooks.Processing;

public class WebhookUpdateMessagesIngestion
{
    private readonly PublisherClient _publisher;

    public WebhookUpdateMessagesIngestion()
    {
        _publisher = PublisherClient.Create(new TopicName(Env.GoogleProjectName, Env.PubSubUpdatesIngestionTopicName));
    }

    public async Task Ingest(string requestBody)
    {
        await _publisher.PublishAsync(requestBody);
    }
}