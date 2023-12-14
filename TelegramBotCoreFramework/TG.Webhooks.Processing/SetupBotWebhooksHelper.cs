using Helpers;
using Telegram.Bot;

namespace TG.Webhooks.Processing;

public class SetupBotWebhooksHelper
{
    private readonly TelegramBotClient _botClient;

    public SetupBotWebhooksHelper(TelegramBotClient botClient)
    {
        _botClient = botClient;
    }

    public async Task Setup()
    {
        var handleUpdateFunctionUrl = $"{Env.WebAppUrl}/LlBotsUpdateProcess";
        await _botClient.SetWebhookAsync(handleUpdateFunctionUrl);
    }

    public async Task Clear()
    {
        await _botClient.DeleteWebhookAsync(false);
    }
}