using System.Diagnostics;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Helpers.PredefinedChannels;

public static class GeneticChannelHelpers
{
    public static Task<Message> SendMessageToChannel(this TelegramBotClient botClient, Message message, ChatId chatId,
        int? messageThreadId = null, int? replyToMessageId = null)
    {
        switch (message.Type)
        {
            case MessageType.Text:
                return botClient.SendTextMessageAsync(
                    chatId: chatId,
                    messageThreadId: messageThreadId,
                    text: message.GetHTML(),
                    parseMode: ParseMode.Html,
                    disableWebPagePreview: true,
                    replyToMessageId: replyToMessageId,
                    replyMarkup: message.ReplyMarkup);
            case MessageType.Photo:
                Debug.Assert(message.Photo != null, "message.Photo != null");
                return botClient.SendPhotoAsync(
                    chatId: chatId,
                    messageThreadId: messageThreadId,
                    photo: new InputFileId(message.Photo.Last().FileId),
                    caption: message.GetHTML(),
                    parseMode: ParseMode.Html,
                    replyToMessageId: replyToMessageId,
                    replyMarkup: message.ReplyMarkup);
            case MessageType.Video:
                Debug.Assert(message.Video != null, "message.Video != null");
                return botClient.SendVideoAsync(
                    chatId: chatId,
                    messageThreadId: messageThreadId,
                    video: new InputFileId(message.Video.FileId),
                    duration: message.Video.Duration,
                    width: message.Video.Width,
                    height: message.Video.Height,
                    caption: message.GetHTML(),
                    parseMode: ParseMode.Html,
                    replyToMessageId: replyToMessageId,
                    replyMarkup: message.ReplyMarkup
                );
            case MessageType.Document:
                Debug.Assert(message.Document != null, "message.Document != null");
                return botClient.SendDocumentAsync(
                    chatId,
                    messageThreadId: messageThreadId,
                    document: new InputFileId(message.Document.FileId),
                    caption: message.GetHTML(),
                    parseMode: ParseMode.Html,
                    replyToMessageId: replyToMessageId,
                    replyMarkup: message.ReplyMarkup
                );
            case MessageType.Sticker:
                return botClient.SendStickerAsync(
                    chatId: chatId,
                    messageThreadId: messageThreadId,
                    sticker: new InputFileId(message.Sticker.FileId),
                    replyMarkup: message.ReplyMarkup,
                    replyToMessageId: replyToMessageId,
                    disableNotification: true
                );
            case MessageType.Animation:
                Debug.Assert(message.Animation != null, "message.Animation != null");
                return botClient.SendAnimationAsync(
                    chatId: chatId,
                    messageThreadId: messageThreadId,
                    animation: new InputFileId(message.Animation.FileId),
                    caption: message.GetHTML(),
                    parseMode: ParseMode.Html,
                    replyToMessageId: replyToMessageId,
                    replyMarkup: message.ReplyMarkup
                );
            case MessageType.Audio: 
                Debug.Assert(message.Audio != null, "message.Audio != null");
                return botClient.SendAudioAsync(
                    chatId: chatId,
                    messageThreadId: messageThreadId,
                    audio: new InputFileId(message.Audio.FileId),
                    caption: message.GetHTML(),
                    parseMode: ParseMode.Html,
                    replyToMessageId: replyToMessageId,
                    replyMarkup: message.ReplyMarkup
                );
            case MessageType.Voice:
                Debug.Assert(message.Voice != null, "message.Voice != null");
                return botClient.SendVoiceAsync(
                    chatId: chatId,
                    messageThreadId: messageThreadId,
                    voice: new InputFileId(message.Voice.FileId),
                    caption: message.GetHTML(),
                    parseMode: ParseMode.Html,
                    replyToMessageId: replyToMessageId,
                    replyMarkup: message.ReplyMarkup
                );
            case MessageType.VideoNote:
                Debug.Assert(message.VideoNote != null, "message.VideoNote != null");
                return botClient.SendVideoNoteAsync(
                    chatId: chatId,
                    messageThreadId: messageThreadId,
                    videoNote: new InputFileId(message.VideoNote.FileId),
                    duration: message.VideoNote.Duration,
                    length: message.VideoNote.Length,
                    replyToMessageId: replyToMessageId,
                    replyMarkup: message.ReplyMarkup
                );
            default:
                return botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    messageThreadId: message.MessageThreadId,
                    text: $"Нажаль, цей тип повідомлення (<code>{message.Type}</code>) не підтримується для комунікації через бота.",
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                    replyToMessageId: message.MessageId
                );
        }
    }
}