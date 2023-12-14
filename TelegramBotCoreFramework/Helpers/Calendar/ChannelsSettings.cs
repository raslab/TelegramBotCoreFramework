using Google.Cloud.Firestore;

namespace Helpers;

public class ChannelsSettings
{
    public const string CrossPrCreoUlrKey = "CrossPrCreoUrl";
    public const string AdSellCreoUrlKey = "AdSellCreoUrl";
    public const string LinkForFriendKey = "LinkForFriend";
    public const string WelcomeBotLinksKey = "WelcomeBotLinks";
    public const string LinkForTgStatKey = "LinkForTgStat";
    public const string LinkOnTgStatKey = "LinkOnTgStat";
    public const string CpmFullCostKey = "CpmFullCost";
    public const string CpmDiscountCostKey = "CpmDiscountCost";
    
    [FirestoreData]
    private class ChannelsList
    {
        [FirestoreProperty] public ChannelSettingsDto[] ChannelSettings { get; set; } = Array.Empty<ChannelSettingsDto>();
        [FirestoreProperty] public ChannelSettingsDto? CommunicationChannel { get; set; } = null;
    }
    
    
    private readonly ConfigurationStorage _configurationStorage;
    private ChannelsList? _channelsList;

    public ChannelSettingsDto[] ChannelSettings => _channelsList?.ChannelSettings ?? Array.Empty<ChannelSettingsDto>();
    public ChannelSettingsDto? CommunicationChannel => _channelsList?.CommunicationChannel;

    public ChannelsSettings(ConfigurationStorage configurationStorage)
    {
        _configurationStorage = configurationStorage;
    }

    public async Task LoadSchedule()
    {
        if (_channelsList == null)
            _channelsList = await _configurationStorage.Get<ChannelsList>();
        if (_channelsList == null)
            _channelsList = new ChannelsList();
    }

    public async Task<ChannelSettingsDto> AddChannel(long channelId, string channelTitle, string channelName, string shortTitle)
    {
        if (_channelsList == null)
            throw new Exception("Schedule must to be loaded before using!");
        
        var c = _channelsList.ChannelSettings;
        Array.Resize(ref c, c.Length + 1);
        c[^1] = new ChannelSettingsDto()
        {
            ChannelId = channelId,
            ChannelUserName = channelName,
            ShortTitle = shortTitle,
            FullTitle = channelTitle
        };
        _channelsList.ChannelSettings = c;
        await _configurationStorage.Push(_channelsList);
        return c[^1];
    }

    public async Task RemoveChannel(ChannelSettingsDto channel)
    {
        if (_channelsList == null)
            throw new Exception("Schedule must to be loaded before using!");

        _channelsList.ChannelSettings = _channelsList.ChannelSettings.Where(c => c.ChannelId != channel.ChannelId).ToArray();
        await _configurationStorage.Push(_channelsList);
    }

    public async Task UpdateChannel(ChannelSettingsDto channel)
    {
        if (_channelsList == null)
            throw new Exception("Schedule must to be loaded before using!");

        for (int i = 0; i < _channelsList.ChannelSettings.Length; i++)
        {
            if (_channelsList.ChannelSettings[i].ChannelId == channel.ChannelId)
                _channelsList.ChannelSettings[i] = channel;
        }
        
        await _configurationStorage.Push(_channelsList);
    }

    public async Task<ChannelSettingsDto> SetCommunicationChannel(long channelId, string channelTitle, string channelName, string shortTitle)
    {
        if (_channelsList == null)
            throw new Exception("Schedule must to be loaded before using!");
        
        _channelsList.CommunicationChannel = new ChannelSettingsDto()
        {
            ChannelId = channelId,
            ChannelUserName = channelName,
            ShortTitle = shortTitle,
            FullTitle = channelTitle
        };
        await _configurationStorage.Push(_channelsList);
        return _channelsList.CommunicationChannel;
    }
}