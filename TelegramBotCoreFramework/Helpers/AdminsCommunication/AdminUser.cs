using Helpers.PredefinedChannels;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Helpers.AdminsCommunication;

public class AdminUser
{
    private readonly AdminProfileDto _userDto;
    private readonly TelegramBotClient _telegramBotClient;
    private readonly FirestoreRepository<AdminProfileDto> _adminsRepository;
    private readonly ILogger _logger;

    public AdminUser(AdminProfileDto userDto, TelegramBotClient telegramBotClient,
        FirestoreRepository<AdminProfileDto> adminsRepository, ILogger logger)
    {
        _userDto = userDto;
        _telegramBotClient = telegramBotClient;
        _adminsRepository = adminsRepository;
        _logger = logger;
    }

    public AdminProfileDto Data => _userDto;

    public async Task SendMessage(string message, ParseMode? parseMode = ParseMode.Html, InlineKeyboardMarkup? replyMarkup = null,
        AdminMessagesRemovingPolicy removeAt = AdminMessagesRemovingPolicy.RemoveOnNextCommand,
        string? route = null, int? replyToMessageId = null, int[]? alsoRemoveThisMessagesAtRouteExit = null, bool dontRemovePreviousButtons = false)
    { 
        try
        {
            List<Task> tasks = new List<Task>();
            
            if (!string.IsNullOrEmpty(route) && _userDto.LastRoute != route)
            {
                foreach (var mId in _userDto.MessagesToRemoveAtNextCommand)
                {
                    tasks.Add(_telegramBotClient.DeleteMessageAsync(_userDto.UserId, mId));
                    _userDto.MessagesToCleanMarkupAtNextMessages.Remove(mId);
                }

                _userDto.MessagesToRemoveAtNextCommand.Clear();
            }

            if (!dontRemovePreviousButtons)
            {
                foreach (var mId in _userDto.MessagesToCleanMarkupAtNextMessages)
                {
                    try
                    {
                        tasks.Add(_telegramBotClient.EditMessageReplyMarkupAsync(_userDto.UserId, mId));
                    }
                    catch (Exception e)
                    {
                        _logger.LogCritical($"Error while updating message markup, but who cares?\nmessage: {message}",
                            e);
                    }
                }
            }

            _userDto.MessagesToCleanMarkupAtNextMessages.Clear();
            
            

            if (replyMarkup == null)
            {
                replyMarkup = new InlineKeyboardMarkup(new[]
                {
                    InlineKeyboardButton.WithCallbackData("Головне меню", AdminsController.BackToMainMenuCommandPath)
                });
            }
            
            
            tasks.Add(_telegramBotClient.SendTextMessageAsync(
                chatId: _userDto.UserId,
                text: message,
                parseMode: parseMode,
                disableWebPagePreview: true,
                replyMarkup: replyMarkup
            ).ContinueWith(res =>
            {
                try
                {
                    var m = res.Result;
                    _userDto.MessagesToCleanMarkupAtNextMessages.Add(m.MessageId);
                    if (removeAt == AdminMessagesRemovingPolicy.RemoveOnNextCommand)
                    {
                        _userDto.MessagesToRemoveAtNextCommand.Add(m.MessageId);
                        if (replyToMessageId.HasValue)
                            _userDto.MessagesToRemoveAtNextCommand.Add(replyToMessageId.Value);
                    }

                    if (alsoRemoveThisMessagesAtRouteExit != null)
                    {
                        foreach (var mId in alsoRemoveThisMessagesAtRouteExit)
                        {
                            _userDto.MessagesToRemoveAtNextCommand.Add(mId);
                        }
                    }

                    if (!string.IsNullOrEmpty(route))
                    {
                        _userDto.LastRoute = route;
                    }
                
                    return _adminsRepository.UpdateAsync(_userDto.UserId.ToString(), _userDto);
                }
                catch (Exception e)
                {
                    _logger.LogCritical(
                        $"Помилка під час спроби відправити повідомлення адміну {_userDto.UserId}: <code> {message} </code>",
                        e);
                    throw;
                }
            }));

            await Task.WhenAll(tasks);
        }
        catch (Exception e)
        {
            _logger.LogCritical(
                $"Помилка під час спроби відправити повідомлення адміну {_userDto.UserId}: <code> {message} </code>",
                e);
        }
    }
}