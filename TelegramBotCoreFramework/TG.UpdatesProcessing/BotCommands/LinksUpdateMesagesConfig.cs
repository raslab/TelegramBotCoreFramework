using Telegram.Bot.Types;

namespace TG.UpdatesProcessing.BotCommands;

[Serializable]
public class LinksUpdateMesagesConfig
{
    internal static LinksUpdateMesagesConfig Empty => new()
    {
        Messages = new List<LinkUpdateMessage>()
    };

    public List<LinkUpdateMessage> Messages;
    public int LastIndex { get; set; }

    internal LinkUpdateMessage AddMessage(string creatorId, Message? message)
    {
        if (Messages == null) Messages = new List<LinkUpdateMessage>();
        var m = new LinkUpdateMessage{
            Index = LastIndex,
            OriginalMessage = message,
            CreateDate = DateTime.UtcNow,
            Creator = creatorId
        };
        Messages.Add(m);
        LastIndex++;
        return m;
    }
}