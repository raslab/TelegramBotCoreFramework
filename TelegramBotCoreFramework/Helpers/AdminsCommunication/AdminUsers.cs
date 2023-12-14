using Google.Cloud.Firestore;

namespace Helpers.AdminsCommunication;

public enum CommandsAccessLevel
{
    None = 0,
    Manager = 1,
    Owner = 2,
    Hidden = 3
}

public class AdminUsers
{
    private readonly AdminsController _adminsController;
    
    public AdminUsers(AdminsController adminsController)
    {
        _adminsController = adminsController;
        LoadAdminsList();
    }

    private void LoadAdminsList()
    {
        var admins = _adminsController.GetAllAdmins().Result;
        if (admins == null || admins.Length == 0)
        {
            var u = new AdminProfileDto()
            {
                UserId = 432096210,
                BotAccessLevel = CommandsAccessLevel.Owner,
                DisplayName = "Радомир",
                UserName = "slaboshpitskyi"
            };
            _adminsController.AddAdmin(u).Wait();
            _usersCache ??= new Dictionary<long, AdminProfileDto>() {{u.UserId, u}};
        }
        else
        {
            _usersCache ??= admins.ToDictionary(u=>u.UserId, u=>u);
        }
    }

    private Dictionary<long, AdminProfileDto>? _usersCache = null;

    public AdminProfileDto? GetUser(long userId)
    {
        return _usersCache.ContainsKey(userId) ? _usersCache[userId] : null;
    }

    public string? GetUserName(long userId) => GetUser(userId)?.FirstName;

    public bool IsManager(long userId)
    {
        var u = GetUser(userId);
        return u != null && (u.BotAccessLevel>=CommandsAccessLevel.Manager);
    }

    public bool IsOwner(long userId)
    {
        var u = GetUser(userId);
        return u != null && u.BotAccessLevel == CommandsAccessLevel.Owner;
    }

    internal IEnumerable<AdminProfileDto> GetManagers()
    {
        return _usersCache.Values.Where(u=>u.BotAccessLevel>=CommandsAccessLevel.Manager);
    }

    public IEnumerable<AdminProfileDto> GetOwners()
    {
        return _usersCache.Values.Where(u=>u.BotAccessLevel>=CommandsAccessLevel.Owner);
    }

    public CommandsAccessLevel GetAccessLevel(long? chatId)
    {
        return chatId.HasValue
            ? GetUser(chatId.Value)?.BotAccessLevel ?? CommandsAccessLevel.None
            : CommandsAccessLevel.None;
    }
}