using Telegram.Bot.Types;

namespace TG.UpdatesProcessing.Helpers;

public interface IBotUpdateThirdPartyProcessor
{
    Task<ProcessResult> Process(Update update);
}

public enum ProcessResult
{
    CanContinue,
    StopProcessing
}