using Helpers.Extensions;
using Microsoft.AspNetCore.Mvc;
using TG.Webhooks.Processing;

namespace core.Controllers;

[ApiController]
[Route("[controller]")]
public class LlBotsUpdateProcessController : ControllerBase
{
    private readonly ILogger<LlBotsSetupBotWebhookController> _logger;
    private readonly WebhookUpdateMessagesIngestion _webhookUpdateMessagesIngestion;

    public LlBotsUpdateProcessController(ILogger<LlBotsSetupBotWebhookController> logger, WebhookUpdateMessagesIngestion webhookUpdateMessagesIngestion)
    {
        _logger = logger;
        _webhookUpdateMessagesIngestion = webhookUpdateMessagesIngestion;
    }

    [HttpPost(Name = "PostLlBotsUpdateProcess")]
    public async Task<string> Post()
    {
        var bodyStr = await Request.GetRawBodyAsync();
        await _webhookUpdateMessagesIngestion.Ingest(bodyStr);
        return "OK";
    }
}