using System.Security.Cryptography;
using System.Text;
using Google.Cloud.Firestore;
using Helpers.PredefinedChannels;
using Newtonsoft.Json;
using TL;
using WTelegram;

namespace Helpers.UserAuth;

public class TgUserAuthController
{
    public const string PhoneNumberKey = "phone_number";
    public const string PasswordKey = "password";
    
    private const string Key = "5mpr#zT7$pNCHmq8B$Ab#%W7wt^B7KE2";
    
    [FirestoreData]
    public class UserAuthData
    {
        [FirestoreProperty]
        public byte[] EncryptedData
        {
            get
            {
                var json = JsonConvert.SerializeObject(Data); 
                return Encrypt(json, Key);
            }
            set
            {
                try
                {
                    var json = Decrypt(value, Key);
                    Data = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                } 
                catch
                {
                    Data = new Dictionary<string, string>();
                }
            }
        }

        public Dictionary<string, string>? Data = new Dictionary<string, string>();
        

        private static byte[] Encrypt(string plainText, string keyString)
        {
            byte[] key = Encoding.UTF8.GetBytes(keyString);
    
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = new byte[16];  // IV is set to 0 for simplicity. For added security, use a random IV and prepend it to the ciphertext.
        
                using (MemoryStream memoryStream = new MemoryStream())
                using (CryptoStream cryptoStream = new CryptoStream(memoryStream, aes.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                    cryptoStream.Write(plainBytes, 0, plainBytes.Length);
                    cryptoStream.FlushFinalBlock();

                    return memoryStream.ToArray();
                }
            }
        }

        private static string Decrypt(byte[] cipherBytes, string keyString)
        {
            byte[] key = Encoding.UTF8.GetBytes(keyString);

            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = new byte[16];

                using (MemoryStream memoryStream = new MemoryStream())
                using (CryptoStream cryptoStream = new CryptoStream(memoryStream, aes.CreateDecryptor(), CryptoStreamMode.Write))
                {
                    cryptoStream.Write(cipherBytes, 0, cipherBytes.Length);
                    cryptoStream.FlushFinalBlock();

                    return Encoding.UTF8.GetString(memoryStream.ToArray());
                }
            }
        }
    }
    
    
    private readonly ConfigurationStorage _configurationStorage;
    private readonly LoggingChannel _loggingChannel;
    private readonly FirestoreSessionStorage _firestoreSessionStorage;

    public Dictionary<string, string> UserData => _authData!.Data!;
    private UserAuthData? _authData = null;
    private string? _code2Fa = null;
    private Action _2faHandler;
    private Client? _userClient;

    public WTelegram.Client? UserClient => _userClient;

    public TgUserAuthController(ConfigurationStorage configurationStorage, 
        LoggingChannel loggingChannel, FirestoreSessionStorage firestoreSessionStorage)
    {
        _configurationStorage = configurationStorage;
        _loggingChannel = loggingChannel;
        _firestoreSessionStorage = firestoreSessionStorage;

        Task.Run(async () =>
        {
            await CreateClient();
            if (_authData == null)
                _authData = await _configurationStorage.Get<UserAuthData>() ?? new UserAuthData();
        });
    }

    private async Task CreateClient()
    {
        try
        {
            _userClient = new Client(UserAuthConfig, _firestoreSessionStorage);
        }
        catch (Exception e)
        {
            await _loggingChannel.LogExceptionToServiceChannel("Error while creating user client. Resetting session.", e);
            _firestoreSessionStorage.ResetSession();
        }
    }

    public bool IsLoggedIn()
    {
        if (UserClient == null)
        {
            CreateClient();
        }
        if (UserClient == null) return false;
        return UserClient.UserId != 0;
    }
    
    
    public async Task Logout()
    {
        await _userClient.Auth_LogOut();
    }

    public Task SetPhone(string? text)
    {
        _authData.Data[PhoneNumberKey] = text;
        return _configurationStorage.Push(_authData);
    }

    public Task SetPassword(string? text)
    {
        _authData.Data[PasswordKey] = text;
        return _configurationStorage.Push(_authData);
    }
    
    private string UserAuthConfig(string what)
    {
        switch (what)
        {
            case "api_id": return Env.TelegramAppId;
            case "api_hash": return Env.TelegramAppSecret;
            // case "phone_number": return "+1234567890";
            case "verification_code":
                _2faHandler?.Invoke();
                while (true)
                {
                    Task.Delay(1000).Wait();
                    if (!string.IsNullOrEmpty(_code2Fa))
                    {
                        var code = _code2Fa;
                        _code2Fa = null;
                        return code;
                    }
                }
            // case "first_name": return "Radomyr";      // if sign-up is required
            // case "last_name": return "";        // if sign-up is required
            // case "password": return "*******";     // if user has enabled 2FA
            // case "session_pathname": return "/etc/sessions/tg_session.dat";
            default: return (_authData?.Data.ContainsKey(what)?? false) ? _authData.Data[what] : null;
        }
    }

    public void Set2Fa(string? text)
    {
        _code2Fa = text;
    }

    public void Set2FaHandler(Action activationCodeRequest)
    {
        _2faHandler = activationCodeRequest;
    }
}