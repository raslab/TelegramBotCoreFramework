using Google.Cloud.Firestore;

namespace Helpers;

[FirestoreData]
public class ChannelSettingsDto
{
    [FirestoreProperty]
    public long ChannelId { get; set; }
    
    [FirestoreProperty]
    public string ChannelUserName { get; set; } = String.Empty;
    
    [FirestoreProperty]
    public string ShortTitle { get; set; } = String.Empty;
    
    [FirestoreProperty]
    public string FullTitle { get; set; } = String.Empty;

    [FirestoreProperty] 
    public Dictionary<string, string> Params { get; set; } = new Dictionary<string, string>();

    public string GetHtmlUrl(bool useShortName = false, int? messageId = null, string? ifExistsGetLinkFromParam = ChannelsSettings.AdSellCreoUrlKey)
    {
        if (messageId == null && !string.IsNullOrEmpty(ifExistsGetLinkFromParam) && Params.ContainsKey(ifExistsGetLinkFromParam))
        {
            return $"<a href=\"{Params[ifExistsGetLinkFromParam]}\">{(useShortName?ShortTitle:FullTitle)}</a>"; 
        }
        
        if (!string.IsNullOrEmpty(ChannelUserName))
        {
            return $"<a href=\"https://t.me/{ChannelUserName}{(messageId.HasValue?$"/{messageId.Value}":"")}\">{(useShortName?ShortTitle:FullTitle)}</a>";
        }
        return $"<a href=\"https://t.me/c/{new String(ChannelId.ToString().Skip(4).ToArray())}{(messageId.HasValue?$"/{messageId.Value}":"")}\">{(useShortName?ShortTitle:FullTitle)}</a>";
    }

    public string GetUrl(string? ifExistsGetLinkFromParam = ChannelsSettings.LinkForFriendKey)
    {
        if (!string.IsNullOrEmpty(ifExistsGetLinkFromParam) && Params.ContainsKey(ifExistsGetLinkFromParam))
        {
            return Params[ifExistsGetLinkFromParam]; 
        }
        if (!string.IsNullOrEmpty(ChannelUserName))
        {
            return $"https://t.me/{ChannelUserName}";
        }
        return $"https://t.me/c/{new String(ChannelId.ToString().Skip(4).ToArray())}";
    }
}