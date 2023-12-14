using System.Text.RegularExpressions;
using Google.Cloud.Firestore;
using Helpers;
using Helpers.AdminsCommunication;
using Helpers.Extensions;
using Helpers.UserAuth;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TG.UpdatesProcessing.BotCommands;

[FirestoreData]
public class MessageLinksEditingDto
{
    [FirestoreProperty] public string MessageJson { get; set; }
    [FirestoreProperty] public string LinkName { get; set; }
}

public class UpdateLinksInCreativeBotCommand : BotCommandControllerBase
{
    private readonly IUserInputAwaiting _userInputAwaiting;
    private readonly ChannelsInfoParser _channelsInfoParser;
    private readonly TgUserAuthController _tgUserAuthController;
    private readonly FirestoreRepository<MessageLinksEditingDto> _editingRepo;
    public override string CommandName => "🔗 Оновити лінки в креативі";
    public override CommandsAccessLevel AccessLevel => CommandsAccessLevel.Owner;
    public override Type? ParentCommandType => typeof(CreativesGenerationRootBotCommand);

    public UpdateLinksInCreativeBotCommand(TelegramBotClient botClient, IBotCommandsFactory botCommandsFactory,
        AdminsController adminsController, IUserInputAwaiting userInputAwaiting,
        FirestoreDb firestoreDb,
        ChannelsInfoParser channelsInfoParser,
        TgUserAuthController tgUserAuthController, AdminUsers adminUsers) 
        : base(botClient, botCommandsFactory, adminsController, adminUsers)
    {
        _userInputAwaiting = userInputAwaiting;
        _channelsInfoParser = channelsInfoParser;
        _tgUserAuthController = tgUserAuthController;
        _editingRepo = new FirestoreRepository<MessageLinksEditingDto>( firestoreDb, "LinkEditingInstances");
    }

    protected override async Task Build()
    {
        AddDefaultShortcut(DefaultPathHandler);
        AddArgShortcut("message", MessageReceivedHandler);
        AddArgShortcut("name", LinkNameReceivedHandler);
        AddArgShortcut("type_open", LinkTypeReceivedHandler);
        AddArgShortcut("type_private", LinkTypeReceivedHandler);
    }

    private async Task<CommandResult> DefaultPathHandler(Update update, string[]? args, string? reRoutedForPath)
    {
        if (!_tgUserAuthController.IsLoggedIn())
        {
            await ComposeMessage(update)
                .SetText("Ця функція доступна тільки з підключеним адмінським аканутом, " +
                         "так як телеграм не дозволяє ботам отримувати інформацію з посилань. Якщо ви хочете скористатись цією функцією - зайдіть в налаштування бота " +
                         "і підключіть акаунт")
                .SetNeedUpMenuButton()
                .SetNeedMainMenuButton()
                .Send();
            return CommandResult.Ok;
        }
        
        await _userInputAwaiting.RequestUserInput(update.GetChatId(), 
            "Перешліть мені повідомлення в котрому ви хочете оновити посилання",
            MyPath, MyPath, new [] {"message"}, 
            alsoRemoveThisMessagesAtRouteExit: new int[]{ update.GetMessageId() });
        return CommandResult.Ok;
    }
    
    
    private async Task<CommandResult> MessageReceivedHandler(Update update, string[]? args, string? reRoutedForPath)
    {
        var json = JsonConvert.SerializeObject(update.Message);
        await _editingRepo.UpdateAsync(update.GetChatId().ToString(), new MessageLinksEditingDto()
        {
            MessageJson = json
        });
        
        await _userInputAwaiting.RequestUserInput(update.GetChatId(), 
            "Введіть назву для нового посилання (як варіант, назву каналу куди ви робите рекламу). Назва повинна бути до 15 латинських симовлів або цифр і нижнього підчеркування.",
            MyPath, MyPath, new [] {"name"}, 
            alsoRemoveThisMessagesAtRouteExit: new int[]{ update.GetMessageId() });
        return CommandResult.Ok;
    }
    
    
    private async Task<CommandResult> LinkNameReceivedHandler(Update update, string[]? args, string? reRoutedForPath)
    {
        var linkName = update.Message?.Text;
        var linkNameRegex = new Regex(@"^[a-zA-Z0-9_\-]{1,15}");
        if (!linkNameRegex.IsMatch(linkName))
        {
            await _userInputAwaiting.RequestUserInput(update.GetChatId(), 
                "Невірний формат назви, спробуйте ще раз. Назва повинна бути до 15 латинських симовлів або цифр і нижнього підчеркування.",
                MyPath, MyPath, new [] {"name"}, 
                alsoRemoveThisMessagesAtRouteExit: new int[]{ update.GetMessageId() });
            return CommandResult.Ok;
        }

        var dto = await _editingRepo.GetAsync(update.GetChatId().ToString());
        dto.LinkName = linkName;
        await _editingRepo.UpdateAsync(update.GetChatId().ToString(), dto);
        
        await PromptUserDialogForCurrentPath(update, 
            "Зробити закрите посилання із апрувом заявки від адміна (або бота)?", "type_private", "type_open");
        return CommandResult.Ok;
    }

    private async Task<CommandResult> LinkTypeReceivedHandler(Update update, string[]? args, string? reRoutedForPath)
    {
        try
        {
            var privateLinkRequested = args[0] == "type_private";
            var dto = await _editingRepo.GetAsync(update.GetChatId().ToString());
            var message = JsonConvert.DeserializeObject<Message>(dto.MessageJson);
            var entities = message.CaptionEntities ?? message.Entities;
            var links = entities.Where(e=>e.Type == Telegram.Bot.Types.Enums.MessageEntityType.TextLink).Select(e=>e.Url).Distinct().ToArray();
            var linkName = $"{dto.LinkName}_{DateTime.UtcNow:ddMMyyyyHHmm}";
            var migratedLinks = await _channelsInfoParser.MigrateLinks(links, linkName, privateLinkRequested);

            var textToSend = message.GetHTML();
            foreach (var linkInfo in migratedLinks)
            {
                while (textToSend.Contains(linkInfo.originalLink))
                {
                    textToSend = textToSend.Replace(linkInfo.originalLink, linkInfo.migratedLink);
                }
            }
            
            var sentMessage = await SendMessageBackWithUpdatedText(update?.Message?.Chat?.Id ?? update?.CallbackQuery?.Message?.Chat?.Id, message, textToSend);

            if (message.ReplyMarkup?.InlineKeyboard?.Sum(l=>l.Count(b=>!string.IsNullOrEmpty(b.Url)))>0)
            {
                var buttonsText = string.Join("", message.ReplyMarkup.InlineKeyboard.Select(keyboard =>
                {
                    return string.Join(" | ", keyboard.Where(u=>!string.IsNullOrEmpty(u.Url)).Select(button =>
                    {
                        var url = button.Url;
                            
                        foreach (var linkInfo in migratedLinks)
                        {
                            if (linkInfo.originalLink == url)
                            {
                                url = linkInfo.migratedLink;
                                break;
                            }
                        }

                        return $"{button.Text} - {url}\n";
                    }));
                }));

                await ComposeMessage(update)
                    .SetText("Кнопки:")
                    .Send();

                await ComposeMessage(update)
                    .SetText(buttonsText)
                    .Send();
            }

            await ComposeMessage(update)
                .SetText($"Лінки оновлено!\nНазіва лінки: <code>{linkName}</code>")
                .RegisterMessageIdToRemoveAtPathExit(sentMessage.MessageId)
                .SetNeedUpMenuButton()
                .SetNeedMainMenuButton()
                .Send();
            
        }
        catch (Exception e)
        {
            if (e.Message.Contains("FLOOD_WAIT_"))
            {
                
                await ComposeMessage(update)
                    .SetText($"Занадто багато запитів на API телеграма. Спробуйте повторити через пів години.")
                    .SetNeedUpMenuButton()
                    .SetNeedMainMenuButton()
                    .Send();
            }
            else throw;
        }

        return CommandResult.Ok;
    }
    
    
    private Task<Message> SendMessageBackWithUpdatedText(long? chatId, Message originalMessage, string textToSend, bool disableWebPagePreview = true)
    {
        switch (originalMessage.Type)
        {
            case Telegram.Bot.Types.Enums.MessageType.Text:
            {
                var text = textToSend;
                return BotClient.SendTextMessageAsync(
                    chatId,
                    text,
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                    disableWebPagePreview: disableWebPagePreview
                );
            }

            case Telegram.Bot.Types.Enums.MessageType.Photo:
            {
                var text = textToSend;
                return BotClient.SendPhotoAsync(
                    chatId,
                    new InputFileId(originalMessage.Photo.Last().FileId),
                    null,
                    text,
                    Telegram.Bot.Types.Enums.ParseMode.Html
                );
            }

            case Telegram.Bot.Types.Enums.MessageType.Sticker:
            {
                return BotClient.SendStickerAsync(
                    chatId,
                    new InputFileId(originalMessage.Sticker.FileId)
                );
            }

            case Telegram.Bot.Types.Enums.MessageType.Document:
            {
                var text = textToSend;
                return BotClient.SendDocumentAsync(
                    chatId,
                    new InputFileId(originalMessage.Document.FileId),
                    null,
                    new InputFileId(originalMessage.Document.Thumbnail.FileId),
                    text,
                    Telegram.Bot.Types.Enums.ParseMode.Html
                );
            }

            default: 
                return BotClient.SendTextMessageAsync(
                    chatId,
                    $"Нажаль, цей тип повідомлення (<code>{originalMessage.Type}</code>) не підтримується для відповіді контакту.",
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                    disableWebPagePreview: disableWebPagePreview
                );
        }
    }
}