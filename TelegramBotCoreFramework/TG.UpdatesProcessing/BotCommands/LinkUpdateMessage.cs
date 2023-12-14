using Telegram.Bot.Types;

namespace TG.UpdatesProcessing.BotCommands;

[Serializable]
public class LinkUpdateMessage
{
    public int Index { get; set; }
    public DateTime CreateDate { get; set; }
    public string Creator {get; set;} = String.Empty;
    public Message? OriginalMessage { get; set; } = null;
    public string UpdatedMessage {get; set;} = String.Empty;
}