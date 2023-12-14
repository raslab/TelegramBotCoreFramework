using Microsoft.AspNetCore.Mvc;
using TG.Webhooks.Processing;

namespace core.Controllers;

[ApiController]
[Route("[controller]")]
public class LlBotsSetupBotWebhookController : ControllerBase
{
    private readonly ILogger<LlBotsSetupBotWebhookController> _logger;
    private readonly SetupBotWebhooksHelper _setupBotWebhooksHelper;

    public LlBotsSetupBotWebhookController(ILogger<LlBotsSetupBotWebhookController> logger, SetupBotWebhooksHelper setupBotWebhooksHelper)
    {
        _logger = logger;
        _setupBotWebhooksHelper = setupBotWebhooksHelper;
    }

    [HttpGet(Name = "GetLlBotsSetupBotWebhook"), HttpPost(Name = "PostLlBotsSetupBotWebhook")]
    public async Task<string> Setup()
    {
        await _setupBotWebhooksHelper.Setup();
        return "ok";
    }
}