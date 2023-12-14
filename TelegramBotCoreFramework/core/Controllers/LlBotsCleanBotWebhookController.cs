using Microsoft.AspNetCore.Mvc;
using TG.Webhooks.Processing;

namespace core.Controllers;

[ApiController]
[Route("[controller]")]
public class LlBotsCleanBotWebhookController : ControllerBase
{
    private readonly ILogger<LlBotsSetupBotWebhookController> _logger;
    private readonly SetupBotWebhooksHelper _setupBotWebhooksHelper;

    public LlBotsCleanBotWebhookController(ILogger<LlBotsSetupBotWebhookController> logger, SetupBotWebhooksHelper setupBotWebhooksHelper)
    {
        _logger = logger;
        _setupBotWebhooksHelper = setupBotWebhooksHelper;
    }

    [HttpGet(Name = "GetLlBotsCleanBotWebhook"), HttpPost(Name = "PostLlBotsCleanBotWebhook")]
    public async Task<string> Clean()
    {
        await _setupBotWebhooksHelper.Clear();
        return "ok";
    }
}