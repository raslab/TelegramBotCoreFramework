using Google.Cloud.Firestore;
using Helpers;
using Newtonsoft.Json;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace CommunicationChat.BotPrivateCommunication;

public class WelcomeBotSettings
{
    private readonly FirestoreRepository<WelcomeBotSettingsDto> _settingsRepository;
    private readonly string _settingsId;

    public enum BotRequestsApproveMode
    {
        Immediate,
        Deffered
    }
    
    [FirestoreData]
    public class WelcomeBotSettingsDto
    {
        [FirestoreProperty] public long BotUserId { get; set; }
        [FirestoreProperty] public string? BotAccessToken { get; set; }
        [FirestoreProperty] public ChannelSettingsDto? ProxyChat { get; set; } = null;
        [FirestoreProperty] public BotRequestsApproveMode BotRequestsApproveMode { get; set; }
        [FirestoreProperty, Obsolete] public string? CaptchaMessage { get; set; }
        [FirestoreProperty] public string? CaptchaJson { get; set; }
        [FirestoreProperty, Obsolete] public string[] WelcomeSequence { get; set; } = Array.Empty<string>();
        [FirestoreProperty] public string? WelcomeSequenceJson { get; set; }

        public static WelcomeBotSettingsDto GetDefault(ChannelSettingsDto[] channelSettings)
        {
            var welcomeSequence = new Message[]
            {
                JsonConvert.DeserializeObject<Message>("{\"message_id\":2308,\"from\":{\"id\":432096210,\"is_bot\":false,\"first_name\":\"Radomyr\",\"username\":\"slaboshpitskyi\",\"language_code\":\"uk\"},\"chat\":{\"id\":432096210,\"first_name\":\"Radomyr\",\"username\":\"slaboshpitskyi\",\"type\":\"private\"},\"date\":1695422061,\"text\":\"\ud83c\udf89 Привіт! \ud83c\udf89\\n\\n\ud83d\ude80 Ласкаво просимо до нашої мережі каналів \ud83c\udf10.\\n\\n\ud83d\udcf2 Завдяки нашому боту ви завжди будете в курсі всіх новин та оновлень.\\n\\n\ud83e\udd16 Цей бот - ваш незамінний помічник! Він надсилатиме вам важливі повідомлення та корисну інформацію. \\n\\n\ud83d\udc65 Якщо у вас виникнуть проблеми або питання, просто напишіть повідомлення цьому боту, і наш менеджер зв'яжеться з вами якнайшвидше.\\n\\n\ud83d\udcbc Ми прагнемо створити якісний контент для вас, але для його підтримки ми іноді надсилатимемо рекламні повідомлення. Ваша підтримка - це те, що допомагає нам продовжувати робити наші канали кращими!\\n\\n\ud83d\udd0e Також вам буде надіслано навігаційне меню наших каналів у наступному повідомленні, щоб ви могли легко переглядати їх.\\n\\n\ud83d\udc96 Дякуємо, що обрали нас. Ми сподіваємося, що вам сподобається тут, і ви залишитеся з нами на довгий час!\\n\\n\ud83d\udc8c З повагою, Адміністрація\",\"entities\":[{\"offset\":0,\"length\":15,\"type\":\"bold\"},{\"offset\":111,\"length\":10,\"type\":\"bold\"},{\"offset\":153,\"length\":19,\"type\":\"bold\"},{\"offset\":293,\"length\":21,\"type\":\"bold\"},{\"offset\":455,\"length\":35,\"type\":\"bold\"},{\"offset\":603,\"length\":16,\"type\":\"bold\"}]}"),
                JsonConvert.DeserializeObject<Message>(
                    "{\"message_id\":2315,\"from\":{\"id\":432096210,\"is_bot\":false,\"first_name\":\"Radomyr\",\"username\":\"slaboshpitskyi\",\"language_code\":\"uk\"},\"chat\":{\"id\":432096210,\"first_name\":\"Radomyr\",\"username\":\"slaboshpitskyi\",\"type\":\"private\"},\"date\":1695422186,\"text\":\"\ud83d\udccdНавігаційне меню\\n\\nЩоб вам було зручніше користуватися нашими каналами, ми підготували для вас це навігаційне меню. Просто натисніть на посилання, щоб перейти до відповідного каналу. \\n\\n%URL%\\n\ud83d\udd0d Приємного користування!\",\"entities\":[{\"offset\":0,\"length\":20,\"type\":\"bold\"}]}"),
            };
            
            var urlsStart = welcomeSequence[1].Text.IndexOf("%URL%");
            var urlsToInsert = channelSettings.Select(c => (c.FullTitle + '\n', c.GetUrl(ChannelsSettings.LinkForFriendKey)))
                .ToArray();
            foreach (var valueTuple in urlsToInsert)
            {
                welcomeSequence[1].Text = welcomeSequence[1].Text.Insert(urlsStart, valueTuple.Item1);
                welcomeSequence[1].Entities = welcomeSequence[1].Entities.Append(new MessageEntity()
                {
                    Offset = urlsStart,
                    Length = valueTuple.Item1.Length,
                    Url = valueTuple.Item2,
                    Type = MessageEntityType.TextLink
                }).ToArray();
                urlsStart = welcomeSequence[1].Text.IndexOf("%URL%");
            }
            welcomeSequence[1].Text = welcomeSequence[1].Text.Replace("%URL%", "");
                
            
            
            return new WelcomeBotSettingsDto()
            {
                BotRequestsApproveMode = BotRequestsApproveMode.Immediate,
                CaptchaJson = "{\"message_id\":2301,\"from\":{\"id\":432096210,\"is_bot\":false,\"first_name\":\"Radomyr\",\"username\":\"slaboshpitskyi\",\"language_code\":\"uk\"},\"chat\":{\"id\":432096210,\"first_name\":\"Radomyr\",\"username\":\"slaboshpitskyi\",\"type\":\"private\"},\"date\":1695421677,\"text\":\"\ud83c\udf89 Привіт! \ud83c\udf89\\n\\nЛаскаво просимо до нашої мережі каналів!.\\n\\nЩоб розпочати вашу подорож з нами, будь ласка, натисніть /start у цьому чаті.\",\"entities\":[{\"offset\":0,\"length\":15,\"type\":\"bold\"},{\"offset\":105,\"length\":9,\"type\":\"bold\"},{\"offset\":115,\"length\":6,\"type\":\"bot_command\"}]}",
                WelcomeSequenceJson = JsonConvert.SerializeObject(welcomeSequence)
            };
        }
    }
    
    private readonly ConfigurationStorage _configurationStorage;
    private readonly ChannelsSettings _channelsSettings;

    private WelcomeBotSettingsDto? _dto = null;
    public long BotUserId
    {
        get => _dto!.BotUserId;
        set => _dto!.BotUserId = value;
    }

    public string? BotAccessToken
    {
        get => _dto!.BotAccessToken;
        set => _dto!.BotAccessToken = value;
    }

    public ChannelSettingsDto? ProxyChat
    {
        get => _dto!.ProxyChat;
        set => _dto!.ProxyChat = value;
    }

    public BotRequestsApproveMode RequestsApproveMode
    {
        get => _dto!.BotRequestsApproveMode;
        set => _dto!.BotRequestsApproveMode = value;
    }

    private Message? _captchaMessage = null;
    public Message? CaptchaMessage
    {
        get
        {
            if (_captchaMessage != null)
                return _captchaMessage;
            if (string.IsNullOrEmpty(_dto!.CaptchaJson))
            {
                // for backward compatibility
                _captchaMessage = new Message()
                {
                    Text = _dto!.CaptchaMessage
                };
            }
            else
            {
                _captchaMessage = JsonConvert.DeserializeObject<Message>(_dto!.CaptchaJson);
            }

            return _captchaMessage;
        }
        set
        {
            _captchaMessage = value;
            _dto!.CaptchaJson = JsonConvert.SerializeObject(_captchaMessage);
        }
    }

    private Message[]? _welcomeSequence = null;
    public Message[]? WelcomeSequence
    {
        get
        {
            if (_welcomeSequence != null)
                return _welcomeSequence;
            if (string.IsNullOrEmpty(_dto!.WelcomeSequenceJson))
            {
                // for backward compatibility
                _welcomeSequence = _dto!.WelcomeSequence.Select(s => new Message()
                {
                    Text = s
                }).ToArray();
            }
            else
            {
                _welcomeSequence = JsonConvert.DeserializeObject<Message[]>(_dto!.WelcomeSequenceJson);
            }
            return _welcomeSequence;
        }
        set
        {
            _welcomeSequence = value;
            _dto!.WelcomeSequenceJson = JsonConvert.SerializeObject(_welcomeSequence);
        }
    }

    public WelcomeBotSettings(ConfigurationStorage configurationStorage,
        ChannelsSettings channelsSettings)
    {
        _configurationStorage = configurationStorage;
        _channelsSettings = channelsSettings;
    }

    public WelcomeBotSettings(FirestoreRepository<WelcomeBotSettingsDto> settingsRepository, string settingsId)
    {
        _settingsRepository = settingsRepository;
        _settingsId = settingsId;
    }

    public async Task LoadDefaultIfNeeded()
    {
        if (_dto == null)
        {
            await _channelsSettings.LoadSchedule();
            _dto = await _configurationStorage.Get<WelcomeBotSettingsDto>();
            if (_dto == null)
            {
                _dto = WelcomeBotSettingsDto.GetDefault(_channelsSettings.ChannelSettings);
            }
        }
    }

    public async Task LoadFromCustomSettingsRepo()
    {
        if (_dto == null)
        {
            _dto = await _settingsRepository.GetAsync(_settingsId);
            if (_dto == null)
            {
                _dto = WelcomeBotSettingsDto.GetDefault(new ChannelSettingsDto[0]);
            }
        }
    }

    public Task SaveSettings()
    {
        if (_configurationStorage == null)
        {
            return _settingsRepository.UpdateAsync(_settingsId, _dto);
        }
        else
        {
            return _configurationStorage.Push(_dto);
        }
    }

    public Task ResetCaptchaToDefault()
    {
        _captchaMessage = null;
        _dto.CaptchaJson = WelcomeBotSettingsDto.GetDefault(_channelsSettings.ChannelSettings).CaptchaJson;
        return SaveSettings();
    }

    public Task ResetWelcomeSequenceToDefault()
    {
        _welcomeSequence = null;
        _dto.WelcomeSequenceJson = WelcomeBotSettingsDto.GetDefault(_channelsSettings.ChannelSettings).WelcomeSequenceJson;
        return SaveSettings();
    }
}