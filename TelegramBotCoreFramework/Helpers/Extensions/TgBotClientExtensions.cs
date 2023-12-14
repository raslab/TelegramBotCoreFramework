using Telegram.Bot.Types;

namespace Helpers.Extensions;

public static class TgBotClientExtensions
{
    public static long GetChatId(this Update update)
    {
        return update.GetMessage().Chat.Id;
    }
    
    public static int GetMessageId(this Update update)
    {
        return update?.Message?.MessageId ?? -1;
    }
    
    public static Message GetMessage(this Update update)
    {
        return (update.Message ?? update.CallbackQuery?.Message)!;
    }
}