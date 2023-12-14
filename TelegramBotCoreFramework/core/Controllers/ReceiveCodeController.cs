using System.Text.Unicode;
using Helpers;
using Helpers.Extensions;
using Microsoft.AspNetCore.Mvc;
using TG.Webhooks.Processing;

namespace core.Controllers;

[ApiController]
[Route("[controller]")]
public class ReceiveCodeController : ControllerBase
{
    private readonly ILogger<LlBotsSetupBotWebhookController> _logger;
    private readonly WebhookUpdateMessagesIngestion _webhookUpdateMessagesIngestion;

    public ReceiveCodeController(ILogger<LlBotsSetupBotWebhookController> logger, WebhookUpdateMessagesIngestion webhookUpdateMessagesIngestion)
    {
        _logger = logger;
        _webhookUpdateMessagesIngestion = webhookUpdateMessagesIngestion;
    }
    
    [HttpPost]
    public async Task<IActionResult> Process()
    {
        // userText=asdasd&userId=id

        if (Request.Form.TryGetValue("userText", out var userText) &&
            Request.Form.TryGetValue("userId", out var userId))
        {
            var template =
                "{\"update_id\":123,\"message\":{\"message_id\":17,\"from\":{\"id\":%USER_ID%,\"is_bot\":false,\"first_name\":\"r\",\"username\":\"s\",\"language_code\":\"uk\"},\"chat\":{\"id\":%USER_ID%,\"first_name\":\"r\",\"username\":\"s\",\"type\":\"private\"},\"date\":1690668550,\"text\":\"%TEXT%\"}}";            
            string bodyStr = template.Replace("%USER_ID%", userId).Replace("%TEXT%", userText);
            await _webhookUpdateMessagesIngestion.Ingest(bodyStr);
            return Content("Дякую, відповідь прийнято. Це вікно можна закривати.", "text/html; charset=utf-8");               
        }
        return Content("Щось не те, спробуйте ще раз.", "text/html; charset=utf-8");
    }
}

[ApiController]
[Route("[controller]")]
public class AskCodeController : ControllerBase
{
    [HttpGet]
    public ContentResult Get()
    {
        if (!Request.Query.TryGetValue("userId", out var useId))
        {
            return new ContentResult()
            {
                StatusCode = 200,
                ContentType = "text/html; charset=utf-8",
                Content = "Перевірте посилання."
            };
        }

        return new ContentResult()
        {
            StatusCode = 200,
            ContentType = "text/html; charset=utf-8",
            Content = "Введіть код:" +
                      "<form method=\"post\" action=\"./ReceiveCode\">\n" +
                      "    <input type=\"text\" id=\"userText\" name=\"userText\" required>\n" +
                      "    <button type=\"submit\">Відправити</button>\n" +
                      $"   <input type=\"hidden\" name=\"userId\" value=\"{useId}\">" +
                      "</form>"
        };
    }
}