using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace Helpers.PredefinedChannels;

public class LoggingChannel
{
    private readonly TelegramBotClient _telegramBotClient = new(Env.LoginChannelTelegramBotToken);

    public Task LogMessageToServiceChannel(string message)
    {
        message = message.Replace("<", "&lt;").Replace(">", "&gt;");
        var errorText = $"Повідомлення від бота <b>{Env.ClientName}</b>.\n<code>{message}</code>";
        return LogFormattedMessageToServiceChannel(errorText);
    }
    
    private async Task LogFormattedMessageToServiceChannel(string message)
    {
        await _telegramBotClient.SendTextMessageAsync(
            chatId: Env.LoggingChannelId,
            text: message,
            parseMode: ParseMode.Html,
            disableWebPagePreview: true
        );
    }

    public Task LogExceptionToServiceChannel(string message, Exception e)
    {
        message = message.Replace("<", "&lt;").Replace(">", "&gt;");
        var stackTrace = e.StackTrace?.Replace("<", "&lt;").Replace(">", "&gt;") ?? "empty";
        var errorText = $"При роботі бота <b>{Env.ClientName}</b> сталась серверна помилка.\nКонтекст: <code>{message}</code>\nПомилка: <pre>{e.Message}</pre>\n<pre>{stackTrace}</pre>";
        try
        {
            return LogMessageToServiceChannel(errorText);
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
            throw;
        }
    }
}