using Google.Cloud.Firestore;

namespace Helpers;

[FirestoreData]
public class SupportedLanguageDto
{
    [FirestoreProperty]
    public string Name { get; set; } = string.Empty;
    
    [FirestoreProperty]
    public string Icon { get; set; } = string.Empty;
    
    [FirestoreProperty]
    public string ShortStoryInitialText { get; set; } = string.Empty;
}