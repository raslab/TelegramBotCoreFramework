using Google.Cloud.Firestore;
using Helpers.AdminsCommunication;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace Helpers;

public interface IUserInputAwaiting
{
    public Task RequestUserInput(long userId, string message, string currentRoute, string commandPathTarget,
        string[] commandArgs, int[]? alsoRemoveThisMessagesAtRouteExit = null);
    public Task RequestUserInputWithWeb(long userId, string message, string currentRoute, string commandPathTarget,
        string[] commandArgs, int[]? alsoRemoveThisMessagesAtRouteExit = null);
    Task SetReceivedMessage(long userId, string receivedMessage);
    public Task<string> GetMessageRedirectRouteIfExists(long userId, bool cleanRedirect = true);
    Task<UserInputAwaiting.InputAwaitingEntity> GetMessageRedirectConfigIfExists(long userId, bool cleanRedirect = true);
}

public class UserInputAwaiting : IUserInputAwaiting
{
    [FirestoreData]
    public class InputAwaitingEntity
    {
        [FirestoreProperty]
        public long AwaitingFromUser { get; set; } = -1;
        [FirestoreProperty]
        public string CommandRoute { get; set; } = string.Empty;
        [FirestoreProperty]
        public string LastReceivedMessage { get; set; } = string.Empty;
    }

    [FirestoreData]
    private class InputAwaitingSettings
    {
        [FirestoreProperty]
        public List<InputAwaitingEntity> WaitingList { get; set; } = new List<InputAwaitingEntity>();
    }

    private readonly TelegramBotClient _botClient;
    private readonly ConfigurationStorage _configurationStorage;
    private readonly AdminsController _adminsController;

    public UserInputAwaiting(TelegramBotClient botClient, 
        ConfigurationStorage configurationStorage,
        AdminsController adminsController)
    {
        this._botClient = botClient;
        this._configurationStorage = configurationStorage;
        _adminsController = adminsController;
    }

    private async Task<InputAwaitingSettings> GetSettings()
    {
        return await _configurationStorage.Get<InputAwaitingSettings>() 
                ?? new InputAwaitingSettings() { WaitingList = new List<InputAwaitingEntity>() };
    }

    public async Task RequestUserInput(long userId, string message, string currentRoute, string commandPathTarget,
        string[] commandArgs, int[]? alsoRemoveThisMessagesAtRouteExit = null)
    {
        var settings = await GetSettings();
        var entityIndex = settings.WaitingList.FindIndex(a=>a.AwaitingFromUser == userId);
        var route = commandArgs.Any() ? $"{commandPathTarget}?{string.Join("/", commandArgs)}" : commandPathTarget;
        if (entityIndex>=0)
        {
            settings.WaitingList[entityIndex].CommandRoute = route;
        }
        else
        {
            settings.WaitingList.Add(new InputAwaitingEntity{AwaitingFromUser = userId, CommandRoute = route});
        }
        await _configurationStorage.Push(settings);

        await (await _adminsController.GetAdminUser(userId))?.SendMessage(
            message: message,
            route: currentRoute,
            alsoRemoveThisMessagesAtRouteExit: alsoRemoveThisMessagesAtRouteExit
        )!;
    }
    
    
    public async Task RequestUserInputWithWeb(long userId, string message, string currentRoute, string commandPathTarget, 
        string[] commandArgs, int[]? alsoRemoveThisMessagesAtRouteExit = null)
    {
        var settings = await GetSettings();
        var entityIndex = settings.WaitingList.FindIndex(a=>a.AwaitingFromUser == userId);
        var route = commandArgs.Any() ? $"{commandPathTarget}?{string.Join("/", commandArgs)}" : commandPathTarget;
        if (entityIndex>=0)
        {
            settings.WaitingList[entityIndex].CommandRoute = route;
        }
        else
        {
            settings.WaitingList.Add(new InputAwaitingEntity{AwaitingFromUser = userId, CommandRoute = route});
        }
        await _configurationStorage.Push(settings);

        message += $"\n\nПерейдіть <a href=\"{Env.WebAppUrl}/AskCode?userId={userId}\">за посиланням</a> щоб ввести вашу відповідь.";
        
        await (await _adminsController.GetAdminUser(userId))?.SendMessage(
            message: message,
            route: currentRoute,
            alsoRemoveThisMessagesAtRouteExit: alsoRemoveThisMessagesAtRouteExit
        )!;
    }

    public async Task SetReceivedMessage(long userId, string receivedMessage)
    {
        var settings = await GetSettings();
        var entityIndex = settings.WaitingList.FindIndex(a=>a.AwaitingFromUser == userId);
        if (entityIndex>=0)
        {
            settings.WaitingList[entityIndex].LastReceivedMessage = receivedMessage;
            await _configurationStorage.Push(settings);
        }
    }

    public async Task<string> GetMessageRedirectRouteIfExists(long userId, bool cleanRedirect = true)
    {
        var settings = await GetSettings();
        var entityIndex = settings.WaitingList.FindIndex(a=>a.AwaitingFromUser == userId);
        if (entityIndex>=0)
        {
            var entity = settings.WaitingList[entityIndex];
            if (cleanRedirect)
            {
                settings.WaitingList.RemoveAt(entityIndex);
                await _configurationStorage.Push(settings);
            }
            return entity.CommandRoute;
        }
        return null;
    }

    public async Task<InputAwaitingEntity> GetMessageRedirectConfigIfExists(long userId, bool cleanRedirect = true)
    {
        var settings = await GetSettings();
        var entityIndex = settings.WaitingList.FindIndex(a=>a.AwaitingFromUser == userId);
        if (entityIndex>=0)
        {
            var entity = settings.WaitingList[entityIndex];
            if (cleanRedirect)
            {
                settings.WaitingList.RemoveAt(entityIndex);
                await _configurationStorage.Push(settings);
            }
            return entity;
        }
        return null;
    }
}