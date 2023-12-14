using Helpers.AdminsCommunication;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Helpers.PredefinedChannels;

public class ProjectTeamCommunication
{
    private readonly TelegramBotClient _telegramBotClient;
    private readonly AdminsController _adminsController;
    private readonly AdminUsers _adminUsers;

    public ProjectTeamCommunication(TelegramBotClient telegramBotClient, 
        AdminsController adminsController, AdminUsers adminUsers)
    {
        _telegramBotClient = telegramBotClient;
        _adminsController = adminsController;
        _adminUsers = adminUsers;
    }


    public async Task SendMessageToAllOwners(string message, AdminMessagesRemovingPolicy removeAt = AdminMessagesRemovingPolicy.NotRemove)
    {
        foreach (var owner in _adminUsers.GetOwners())
        {
            var u = await _adminsController.GetAdminUser(owner.UserId);
            await u!.SendMessage(message, removeAt: removeAt, dontRemovePreviousButtons:true);
        }
    }

    public async Task ForwardMessageToAllOwners(long fromChatId, int messageId)
    {
        await Task.WhenAll(_adminUsers.GetOwners()
            .Select(u => _telegramBotClient.ForwardMessageAsync(
                u.UserId,
                fromChatId,
                messageId
            )));
    }

    public async Task SendMessageToAllManagers(string message, AdminMessagesRemovingPolicy removeAt = AdminMessagesRemovingPolicy.NotRemove)
    {
        foreach (var owner in _adminUsers.GetManagers())
        {
            var u = await _adminsController.GetAdminUser(owner.UserId);
            await u!.SendMessage(message, removeAt: removeAt, dontRemovePreviousButtons:true);
        }
    }
}