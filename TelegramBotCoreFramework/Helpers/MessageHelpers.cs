using Telegram.Bot.Types;

namespace Helpers;

public static class MessageHelpers
{
    public static string GetHTML(this Message message)
    {
        var text = message.Text ?? message.Caption ?? "";
        text = text.Replace("<", "诶").Replace(">", "必"); // hack to escape this symbols

        var entities = message.Entities ?? message.CaptionEntities;

        if (entities == null || !entities.Any())
        {
            return text;
        }

        entities = entities.Select(e=>new MessageEntity() {
            Language = e.Language,
            Length = e.Length,
            Offset = e.Offset,
            Type = e.Type,
            Url = e.Url,
            User = e.User
        }).ToArray();

        for (int i = 0; i < entities.Length; i++)
        {
            MessageEntity? entity = entities[i];
            var inserts = EntityToParts(entity, 0, text).ToArray();
            if (inserts == null || inserts.Length != 2)
                continue;
            foreach (var insertPart in inserts.Reverse())
                text = text.Insert(insertPart.position, insertPart.text);

            var l1 = inserts[0].text.Length;
            var l2 = inserts[1].text.Length;
            for (var j = i+1; j < entities.Length; j++)
            {
                var next = entities[j];
                if (next.Offset >= entity.Offset + entity.Length)
                {
                    next.Offset += l1 + l2;
                }
                else if (next.Offset <= entity.Offset && next.Offset + next.Length >= entity.Offset + entity.Length)
                {
                    next.Length += l1 + l2;
                }
                else if (next.Offset <= entity.Offset && next.Offset + next.Length < entity.Offset + entity.Length)
                {
                    next.Offset += l1;
                }
                else if (next.Offset >= entity.Offset & next.Offset + next.Length <= entity.Offset + entity.Length)
                {
                    next.Offset += l1;
                }
                else 
                {
                    // something unpredicted
                }

            }
        }

        text = text.Replace("诶", "&lt;").Replace("必", "&gt;");
        return text;
    }

    private static IEnumerable<(int position, int weight, string text)> EntityToParts(MessageEntity entity, int index, string text)
    {
        switch (entity.Type)
        {
            default:
            case Telegram.Bot.Types.Enums.MessageEntityType.Mention:
            case Telegram.Bot.Types.Enums.MessageEntityType.Hashtag:
            case Telegram.Bot.Types.Enums.MessageEntityType.Cashtag:
            case Telegram.Bot.Types.Enums.MessageEntityType.BotCommand:
            case Telegram.Bot.Types.Enums.MessageEntityType.Url:
            case Telegram.Bot.Types.Enums.MessageEntityType.Email:
            case Telegram.Bot.Types.Enums.MessageEntityType.PhoneNumber:
                return Array.Empty<(int position, int, string text)>();
            case Telegram.Bot.Types.Enums.MessageEntityType.TextMention:
                return new []
                {
                    (entity.Offset, index, $"<a href='tg://user?id={entity.User.Id}'>"),
                    (entity.Offset + entity.Length, index, "</a>")
                };
            case Telegram.Bot.Types.Enums.MessageEntityType.TextLink:
                return new []
                {
                    (entity.Offset,  index, $"<a href='{entity.Url}'>"),
                    (entity.Offset + entity.Length, index, "</a>")
                };
            case Telegram.Bot.Types.Enums.MessageEntityType.Bold:
                return new []
                {
                    (entity.Offset,  index, "<b>"),
                    (entity.Offset + entity.Length, index, "</b>")
                };
            case Telegram.Bot.Types.Enums.MessageEntityType.Italic:
                return new []
                {
                    (entity.Offset,  index, "<i>"),
                    (entity.Offset + entity.Length, index, "</i>")
                };
            case Telegram.Bot.Types.Enums.MessageEntityType.Code:
                return new []
                {
                    (entity.Offset,  index, "<code>"),
                    (entity.Offset + entity.Length, index, "</code>")
                };
            case Telegram.Bot.Types.Enums.MessageEntityType.Pre:
                return new []
                {
                    (entity.Offset,  index, "<pre>"),
                    (entity.Offset + entity.Length, index, "</pre>")
                };
            case Telegram.Bot.Types.Enums.MessageEntityType.Strikethrough:
                return new []
                {
                    (entity.Offset,  index, "<s>"),
                    (entity.Offset + entity.Length, index, "</s>")
                };
            case Telegram.Bot.Types.Enums.MessageEntityType.Underline:
                return new []
                {
                    (entity.Offset,  index, "<u>"),
                    (entity.Offset + entity.Length, index, "</u>")
                };
            case Telegram.Bot.Types.Enums.MessageEntityType.Spoiler:
                return new []
                {
                    (entity.Offset,  index, "<span class=\"tg-spoiler\">"),
                    (entity.Offset + entity.Length, index, "</span>")
                };
        }
    }
}