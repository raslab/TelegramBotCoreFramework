using Analytics.UsersDatabase;
using Helpers;
using Helpers.AdminsCommunication;
using Telegram.Bot;
using Telegram.Bot.Types;
using TG.UpdatesProcessing.BotCommands;

namespace TG.UpdatesProcessing.WelcomeBot;

public class ChatJoinRequestsApprovalBotCommand : BotCommandControllerBase
{
    public override string CommandName => "📔 Поточні заявки";
    public override CommandsAccessLevel AccessLevel => CommandsAccessLevel.Owner;
    public override Type? ParentCommandType => typeof(WelcomeBotRootCommand);
    
    
    private readonly ChannelJoinRequestsProcessor _channelJoinRequestsProcessor;
    private readonly ChannelsSettings _channelsSettings;
    private readonly RequestsBatchApprovalService _requestsBatchApprovalService;
    private readonly SubscribersDatabase _subscribersDatabase;

    public ChatJoinRequestsApprovalBotCommand(TelegramBotClient botClient, IBotCommandsFactory botCommandsFactory,
        AdminsController adminsController, ChannelJoinRequestsProcessor channelJoinRequestsProcessor,
        ChannelsSettings channelsSettings, RequestsBatchApprovalService requestsBatchApprovalService,
        SubscribersDatabase subscribersDatabase, AdminUsers adminUsers) 
        : base(botClient, botCommandsFactory, adminsController, adminUsers)
    {
        _channelJoinRequestsProcessor = channelJoinRequestsProcessor;
        _channelsSettings = channelsSettings;
        _requestsBatchApprovalService = requestsBatchApprovalService;
        _subscribersDatabase = subscribersDatabase;
    }
    protected override async Task Build()
    {
        await _channelsSettings.LoadSchedule();
        
        AddDefaultShortcut(ViewRequestsStatusHandler);
        AddArgShortcut("_", ViewRequestsStatusHandler);
        AddArgShortcut("approve_all", ApproveAllHandler);
        AddArgShortcut("approve_all+", ApproveAllApprovedHandler);
        AddArgShortcut("users_sample", UsersSampleHandler);
    }

    private async Task<CommandResult> ViewRequestsStatusHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var requestsCount = await _channelJoinRequestsProcessor.GetPendingRequestsByChannels();
        var currentApprovalsJobs = _requestsBatchApprovalService.GetCurrentApprovalsJobs();

        var txt = $"Кількість поточних заявок по каналам:\n" +
                  $"{(string.Join("\n", requestsCount.Select(r => $"{r.channel.GetHtmlUrl()} - {(currentApprovalsJobs.Any(j => j.ChannelId == r.channel.ChannelId) ? "В процесі прийому заявок" : r.pendingRequestsCount)}")))}" +
                  $"\n\nВсього: {requestsCount.Sum(r => r.pendingRequestsCount ?? 0)} заявок";
        var m = ComposeMessage(update)
            .SetText(txt)
            .SetNeedUpMenuButton()
            .SetNeedMainMenuButton()
            .SetButtonsInARow(2);

        foreach (var (channel, pendingRequestsCount) in requestsCount)
        {
            if (pendingRequestsCount > 0 && currentApprovalsJobs.All(j=>j.ChannelId != channel.ChannelId))
            {
                var index = Array.FindIndex(_channelsSettings.ChannelSettings, c => c.ChannelId == channel.ChannelId);
                m.AddButtonForCurrentPath($"✅ Прийняти всіх {channel.ShortTitle}", "approve_all", index.ToString());
                m.AddButtonForCurrentPath($"👀 Зразок пдп {channel.ShortTitle}", "users_sample", index.ToString());
            }
        }
        
        await m.Send();
        return CommandResult.Ok;
    }

    private async Task<CommandResult> ApproveAllHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var channel = _channelsSettings.ChannelSettings[int.Parse(args[1])];
        await PromptUserDialogForCurrentPath(update,
            $"Ви впевнені що хочете прийняти всі заявки від користувачів із канала {channel.GetHtmlUrl()}? " +
            $"Після початку прийому, цю дію не можна буде відмінити.", $"approve_all+/{args[1]}", "_");
        return CommandResult.Ok;
    }
    
    private async Task<CommandResult> ApproveAllApprovedHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var channel = _channelsSettings.ChannelSettings[int.Parse(args[1])];
        var job = await _requestsBatchApprovalService.StartAllRequestsApprovalFromChannel(channel);
        await ComposeMessage(update)
            .SetText(
                $"Розмочато процес прийому всіх заявок під канал {channel.GetHtmlUrl()}. Ідентифікатор процесу - {job.Id}. Після завершення процедури, я надішлю звіт з результатами.")
            .SetNeedCurrentMenuButton()
            .SetNeedUpMenuButton()
            .SetNeedMainMenuButton()
            .Send();
        return CommandResult.Ok;
    }
    
    private async Task<CommandResult> UsersSampleHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var channel = _channelsSettings.ChannelSettings[int.Parse(args[1])];
        var subs = await _subscribersDatabase.GetPendingRequestUsers(channel.ChannelId, 30);


        var subsText = string.Join("\n",
            subs.Select((s, i) =>
                $"{i + 1}. {(string.IsNullOrEmpty(s.FirstName) ? "_" : s.FirstName)} {(string.IsNullOrEmpty(s.LastName) ? "_" : s.LastName)}, @{(string.IsNullOrEmpty(s.UserName) ? "_" : s.UserName)}"));
        var txt = $"Показую декілька випадкових заявок на підключення до каналу {channel.GetHtmlUrl()}. " +
                  $"Користувачі відображаються у форматі <code>[first name] [last name] @[username]</code>, до 30 підписників.\n\n" + subsText;
        await ComposeMessage(update)
            .SetText(txt)
            .SetNeedCurrentMenuButton()
            .SetNeedUpMenuButton()
            .SetNeedMainMenuButton()
            .Send();
        return CommandResult.Ok;
    }
}