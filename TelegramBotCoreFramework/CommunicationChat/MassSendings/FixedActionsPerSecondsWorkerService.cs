using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;

namespace CommunicationChat.MassSendings;

public abstract class FixedActionsPerSecondsWorkerService<ActionInput, ActionOutput> : IHostedService, IDisposable
{
    private Timer _timer;

    private readonly ConcurrentQueue<(ActionInput request, TaskCompletionSource<ActionOutput> completionSource)> _messageQueue = new();
    
    protected int ApsRate;

    public FixedActionsPerSecondsWorkerService(int apsRate)
    {
        ApsRate = apsRate;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(ProcessQueue, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        return Task.CompletedTask;
    }

    private void ProcessQueue(object state)
    {
        for (int i = 0; i < ApsRate && !_messageQueue.IsEmpty; i++)
        {
            if (_messageQueue.TryDequeue(out var pair))
            {
                Task.Run(()=>OneAction(pair.request, pair.completionSource));
            }
        }
    }

    protected abstract Task OneAction(ActionInput input, TaskCompletionSource<ActionOutput> outputSource);

    public virtual Task<ActionOutput> EnqueueMessage(ActionInput messageRequest)
    {
        var cs = new TaskCompletionSource<ActionOutput>();
        _messageQueue.Enqueue((messageRequest, cs));
        return cs.Task;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}