using Google.Cloud.Firestore;

namespace Helpers.AdminsCommunication;

[FirestoreData]
public class AdminProfileDto
{
    [FirestoreProperty] public long UserId { get; set; }
    [FirestoreProperty] public CommandsAccessLevel BotAccessLevel { get; set; }
    [FirestoreProperty] public List<int> MessagesToRemoveAtNextCommand { get; set; } = new List<int>();
    [FirestoreProperty] public List<int> MessagesToCleanMarkupAtNextMessages { get; set; } = new List<int>();
    [FirestoreProperty] public string LastRoute { get; set; }
    [FirestoreProperty] public string? FirstName { get; set; }
    [FirestoreProperty] public string? LastName { get; set; }
    [FirestoreProperty] public string? UserName { get; set; }
    [FirestoreProperty] public string DisplayName { get; set; }
}