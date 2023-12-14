using CsvHelper.Configuration.Attributes;
using Google.Cloud.Firestore;
using Helpers.PredefinedChannels;
using Microsoft.Extensions.Logging;
using Telegram.Bot;

namespace Helpers.AdminsCommunication;

public class AdminsController
{
    public const string BackToMainMenuCommandPath = "mm";
    
    private readonly TelegramBotClient _botClient;
    private readonly ILogger<AdminsController> _logger;
    private readonly FirestoreRepository<AdminProfileDto> _adminsRepo;
    private readonly Dictionary<long, AdminUser> _users = new Dictionary<long, AdminUser>();

    public AdminsController(FirestoreDb firestoreDb, TelegramBotClient botClient, 
        ILogger<AdminsController> logger)
    {
        _botClient = botClient;
        _logger = logger;
        _adminsRepo = new FirestoreRepository<AdminProfileDto>(firestoreDb, "AdminUsers");
    }

    public async Task<AdminUser?> GetAdminUser(long userId)
    {
        if (!_users.ContainsKey(userId))
        {
            var dto = await GetDtoForAdmin(userId);
            if (dto != null)
            {
                if (!_users.ContainsKey(userId)) // this check is needed because of async
                {
                    _users.Add(userId, new AdminUser(dto, _botClient, _adminsRepo, _logger));
                }
            }
        }
        return _users[userId];
    }

    private async Task<AdminProfileDto?> GetDtoForAdmin(long userId)
    {
        var dto = await _adminsRepo.GetAsync(userId.ToString());
        return dto;
    }
    
    public async Task<AdminProfileDto[]?> GetAllAdmins()
    {
        var dto = await _adminsRepo.GetAllAsync();
        return dto?.Select(d=>d.Item2).ToArray();
    }

    public async Task AddAdmin(AdminProfileDto adminProfileDto)
    {
        await _adminsRepo.UpdateAsync(adminProfileDto.UserId.ToString(), adminProfileDto);
    }
}