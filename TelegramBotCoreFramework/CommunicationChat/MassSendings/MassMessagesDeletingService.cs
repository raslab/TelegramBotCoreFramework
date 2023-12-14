using Microsoft.Extensions.Hosting;
using Telegram.Bot;

namespace CommunicationChat.MassSendings;


public class MassMessagesDeletingFactory : IHostedService, IDisposable
{
    private readonly Dictionary<TelegramBotClient, MassMessagesDeletingService> _cache =
        new Dictionary<TelegramBotClient, MassMessagesDeletingService>();

    private readonly TelegramBotClient _telegramBotClient;
    private bool _started = false;

    public MassMessagesDeletingFactory(TelegramBotClient telegramBotClient)
    {
        _telegramBotClient = telegramBotClient;
    }


    public MassMessagesDeletingService CreateServiceFor(TelegramBotClient telegramBotClient)
    {
        if (_cache.TryGetValue(telegramBotClient, out var service))
            return service;

        service = new MassMessagesDeletingService();
        service.InitFor(telegramBotClient);
        _cache.Add(telegramBotClient, service);
        if (_started)
            service.StartAsync(default).Wait();
        return service;
    }

    public MassMessagesDeletingService CreateDefault()
    {
        return CreateServiceFor(_telegramBotClient);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _started = true;
        return Task.WhenAll(_cache.Values.Select(s => s.StartAsync(cancellationToken)));
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.WhenAll(_cache.Values.Select(s => s.StopAsync(cancellationToken)));
    }

    public void Dispose()
    {
        foreach (var (key, service) in _cache)
        {
            service.Dispose();
        }
        _cache.Clear();
    }
}

public class MassMessagesDeletingService : FixedActionsPerSecondsWorkerService<(long chatId, int messgeId), bool>
{
    private TelegramBotClient _telegramBotClient;

    public MassMessagesDeletingService() : base(10) { }

    public void InitFor(TelegramBotClient telegramBotClient, int targetApm = 25)
    {
        _telegramBotClient = telegramBotClient;
        base.ApsRate = targetApm;
    }

    protected override async Task OneAction((long chatId, int messgeId) input, TaskCompletionSource<bool> outputSource)
    {
        try
        {
            await _telegramBotClient.DeleteMessageAsync(input.chatId, input.messgeId);
            outputSource.SetResult(true);
        }
        catch (Exception e)
        {
            outputSource.SetException(e);
        }
    }
}