using Google.Cloud.Firestore;

namespace Helpers.UserAuth;


[FirestoreData]
public class TgUserSessionData
{
    [FirestoreProperty] public byte[] SessionData { get; set; } = Array.Empty<byte>();
}

public class FirestoreSessionStorage : Stream
{
    private byte[] _localSessionData;
    private long _position = 0;
    private readonly ConfigurationStorage _configurationStorage;

    public FirestoreSessionStorage(ConfigurationStorage configurationStorage)
    {
        _configurationStorage = configurationStorage;
        // Initialize Firestore and load session data
        LoadSessionData().Wait();
    }

    private async Task LoadSessionData()
    {
        TgUserSessionData tgUserSessionData = await _configurationStorage.Get<TgUserSessionData>() ?? new TgUserSessionData();
        _localSessionData = tgUserSessionData?.SessionData ?? Array.Empty<byte>();
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => true;

    public override long Length => _localSessionData.Length;

    public override long Position
    {
        get => _position;
        set => _position = value;
    }

    public override void Flush()
    {
        var tgUserSessionData = new TgUserSessionData { SessionData = _localSessionData };
        _configurationStorage.Push(tgUserSessionData).Wait();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int bytesRead = 0;
        for (int i = 0; i < count && _position < Length; i++, _position++, bytesRead++)
        {
            buffer[offset + i] = _localSessionData[_position];
        }

        return bytesRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        switch (origin)
        {
            case SeekOrigin.Begin:
                _position = offset;
                break;
            case SeekOrigin.Current:
                _position += offset;
                break;
            case SeekOrigin.End:
                _position = Length + offset;
                break;
        }

        return _position;
    }

    public override void SetLength(long value)
    {
        Array.Resize(ref _localSessionData, (int)value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        for (int i = 0; i < count; i++, _position++)
        {
            if (_position >= Length)
            {
                SetLength(_position + 1);
            }

            _localSessionData[_position] = buffer[offset + i];
        }
        Flush();
    }

    public void ResetSession()
    {
        _localSessionData = Array.Empty<byte>();
        Flush();
    }
}
