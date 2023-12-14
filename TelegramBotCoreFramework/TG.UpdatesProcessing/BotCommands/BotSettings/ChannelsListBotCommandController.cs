using System.Diagnostics;
using System.Text.RegularExpressions;
using Helpers;
using Helpers.AdminsCommunication;
using Helpers.Extensions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace TG.UpdatesProcessing.BotCommands.BotSettings;

public class ChannelsListBotCommandController : BotCommandControllerBase
{
    public override string CommandName => "📋 Список каналів";
    public override CommandsAccessLevel AccessLevel => CommandsAccessLevel.Owner;
    public override Type? ParentCommandType => typeof(BotSettingsSettingsBotCommand);


    private readonly ChannelsSettings _channelsSettings;
    private readonly IUserInputAwaiting _userInputAwaiting;

    public ChannelsListBotCommandController(TelegramBotClient botClient, IBotCommandsFactory botCommandsFactory,
        ChannelsSettings channelsSettings, IUserInputAwaiting userInputAwaiting, AdminsController adminsController,
        AdminUsers adminUsers)
        : base(botClient, botCommandsFactory, adminsController, adminUsers)
    {
        _channelsSettings = channelsSettings;
        _userInputAwaiting = userInputAwaiting;
    }

    protected override async Task Build()
    {
        await _channelsSettings.LoadSchedule();
        AddDefaultShortcut(DefaultHandler);
        AddArgShortcut("add", AddChannelHandler);
        AddArgShortcut("add+", AddChannelForwardedHandler);
        AddArgShortcut("e", EditChannelHandler);
        AddArgShortcut("rm", RemoveChannelHandler);
        AddArgShortcut("rm+", RemoveChannelApprovedHandler);
        AddArgShortcut("es", EditChannelShortNameHandler);
        AddArgShortcut("es+", EditChannelShortNameApprovedHandler);
        AddArgShortcut("ep", EditChannelParamsHandler);
        AddArgShortcut("apa", AddParameterHandler);
        AddArgShortcut("apa+", AddParameterApprovedHandler);
        AddArgShortcut("epa",EditParameterValueHandler);
        AddArgShortcut("epa+",EditParameterValueApprovedHandler);
        AddArgShortcut("rpa", RemoveParameterHandler);
        AddArgShortcut("rpa+", RemoveParameterApprovedHandler);
        AddArgShortcut("comset", SetCommunicationChannelHandler);
        AddArgShortcut("comset+", SetCommunicationChannelForwardedHandler);
    }
    
    private async Task<CommandResult> SetCommunicationChannelHandler(Update update, string[]? args, string? reroutedforpath)
    {
        await _userInputAwaiting.RequestUserInput(update.GetChatId(),
            "Перешліть посилання на повідомлення в группі, формату <code>https://t.me/c/1924387865/1/4</code>. " +
            "Щоб отримати посилання - виділіть  будь-яке повідомлення в групі і натисніть \"Копіювати посилання\". " +
            "Группа повинна бути із включеними тредами, і бот в ній є адміном щоб встановити группу як группу комунікації.\n" +
            "\n ℹ В цей канал бот буде пересилати повідомлення від користувачів. Якщо ви, або будь-хто із вашої команди, відповісте на ці повідомлення в каналі - бот надішле відповідь користувачу від свого імені.", 
            MyPath, MyPath,new [] {"comset+"});
        return CommandResult.Ok;
    }

    private async Task<CommandResult> SetCommunicationChannelForwardedHandler(Update update, string[]? args,
        string? reroutedforpath)
    {
        // link example: https://t.me/c/1924387865/1/4
        var link = update.Message.Text;
        var split = link.Split('/');
        if (split.Length < 5 || !long.TryParse("-100" + split[4], out var channelId))
        {
            await ComposeMessage(update)
                .SetText(
                    "Не можу розпарсити посилання, перевірте що повідомлення було переслано корректно і повторіть знову.")
                .SetNeedCurrentMenuButton()
                .Send();
            return CommandResult.Ok;
        }

        Chat channelInfo;
        try
        {
            channelInfo = await BotClient.GetChatAsync(channelId);
            Debug.Assert(channelInfo.Type == ChatType.Group || channelInfo.Type == ChatType.Supergroup, "Чат повинен бути групою або супергрупою");
            Debug.Assert(channelInfo.IsForum ?? false, "Чат повинен бути групою з увімкнутими тредами");
        }
        catch (Exception e)
        {
            await ComposeMessage(update)
                .SetText(
                    "Не можу ідентифікувати чат. Це може статись при некоректному посилання, або якщо бот не є учасником чату. Перевірте що бот є учасником чату і що посилання на повідомлення корректне і повторіть спробу.\n" +
                    "Якщо це допоможе, то помилка: " + e.Message)
                .SetNeedCurrentMenuButton()
                .Send();
            return CommandResult.Ok;
        }
        var isMeAdmin = false;
        try
        {
            var me = await BotClient.GetMeAsync();
            var member = await BotClient.GetChatMemberAsync(channelId, me.Id);
            isMeAdmin = member.Status == ChatMemberStatus.Administrator;
        }
        catch
        {
            // do nothing
        }

        if (!isMeAdmin)
        {
            await ComposeMessage(update)
                .SetText(
                    "Бот не є адміністратором каналу. Каналом комунікації можна можна вказати тільки канал де бот є адміністратором.")
                .SetNeedCurrentMenuButton()
                .Send();
            return CommandResult.Ok;
        }

        var channel = await _channelsSettings.SetCommunicationChannel(channelId, channelInfo.Title,
            channelInfo.Username, channelInfo.Title);

        await ComposeMessage(update)
            .SetText($"Канал {channel.GetHtmlUrl()} успішно встановнелий як канал для комунікації!")
            .SetButtonsInARow(1)
            .SetNeedMainMenuButton()
            .SetNeedCurrentMenuButton()
            .Send();
        return CommandResult.Ok;
    }

    private async Task<CommandResult> DefaultHandler(Update update, string[]? args, string? reroutedforpath)
    {
        var message = ComposeMessage(update);

        var generalInformation = $"<b>🔊 Список каналів </b>" +
                                 $"\n\n<b>Чат комунікації (треба для проксі)</b>: {(_channelsSettings.CommunicationChannel?.GetHtmlUrl() ?? "Канал не задано")}" +
                                 $"\n\n<b>Канали з контентом</b>: "; 
        
        if ((_channelsSettings.ChannelSettings?.Length ?? 0) == 0)
        {
            message.SetText(generalInformation+"Немає збережених каналів.");
        }
        else
        {
            var channelsList = string.Join("\n",
                _channelsSettings.ChannelSettings?.Select((l, i) => $"{(i + 1)}) {l.GetHtmlUrl()}") ??
                Array.Empty<string>());
            message.SetText($"{generalInformation}\n{channelsList}")
                .AddButtonsForCurrentPath(_channelsSettings.ChannelSettings
                    .Select((l, i) => ($"Редагувати {l.ShortTitle}", $"e/{i.ToString()}")).ToArray());
        }

        await message
            .AddButtonForCurrentPath($"➕ Додати канал", $"add")
            .AddButtonForCurrentPath("☎ Вказати чат комунікації", "comset")
            .SetButtonsInARow(1)
            .SetNeedUpMenuButton()
            .SetNeedMainMenuButton()
            .Send();
        return CommandResult.Ok;
    }
    
    private async Task<CommandResult> AddChannelHandler(Update update, string[]? args, string? reroutedforpath)
    {
        await _userInputAwaiting.RequestUserInput(update.GetChatId(),
            "Перешліть пост із каналу де бот є адміном щоб додати канал", MyPath, MyPath,new [] {"add+"});
        return CommandResult.Ok;
    }

    private async Task<CommandResult> AddChannelForwardedHandler(Update update, string[]? args,
        string? reroutedforpath)
    {
        if (update.Message?.ForwardFromChat == null)
        {

            await ComposeMessage(update)
                .SetText(
                    "Не можу ідентифікувати чат, перевірте що повідомлення було переслано корректно і повторіть знову.")
                .SetNeedCurrentMenuButton()
                .Send();
            return CommandResult.Ok;
        }

        var channelId = update.Message.ForwardFromChat.Id;
        var channelTitle = update.Message.ForwardFromChat.Title;

        var isMeAdmin = false;
        try
        {
            var me = await BotClient.GetMeAsync();
            var member = await BotClient.GetChatMemberAsync(channelId, me.Id);
            isMeAdmin = member.Status == ChatMemberStatus.Administrator;
        }
        catch
        {
            // do nothing
        }

        if (!isMeAdmin)
        {
            await ComposeMessage(update)
                .SetText(
                    "Бот не є адміністратором каналу. В список каналів можна додати тільки канали в яких бот є адміністратором.")
                .SetNeedCurrentMenuButton()
                .Send();
            return CommandResult.Ok;
        }

        var alreadyAdded =
            _channelsSettings.ChannelSettings.FirstOrDefault(c => c.ChannelId == channelId);
        if (alreadyAdded != null)
        {
            await ComposeMessage(update)
                .SetText($"Канал {alreadyAdded.GetHtmlUrl()} вже додано до бази.")
                .SetNeedCurrentMenuButton()
                .Send();
            return CommandResult.Ok;
        }

        var match = new Regex(@"^(?<flag>.{4}) (?<language>\w+) (?<level>[A-Z0-1]{2}) \| (?<flag2>.{4}) Мовна Палата")
            .Match(channelTitle);
        var shortTitle = channelTitle;
        if (shortTitle.Length > 10)
            shortTitle = shortTitle.Substring(0, 7) + "...";

        if (channelTitle.ToLower().Contains("test"))
        {
            shortTitle = "Test channel";
        }
        else if (match.Success)
        {
            shortTitle = $"{match.Groups["flag"]} {match.Groups["level"]}";
        }

        var channel = await _channelsSettings.AddChannel(channelId, channelTitle,
            update.Message.ForwardFromChat.Username, shortTitle);

        await ComposeMessage(update)
            .SetText($"Канал {channel.GetHtmlUrl()} успішно додано до бази!")
            .SetButtonsInARow(1)
            .SetNeedMainMenuButton()
            .SetNeedCurrentMenuButton()
            .Send();
        return CommandResult.Ok;
    }

    private async Task<CommandResult> EditChannelHandler(Update update, string[]? args,
        string? reroutedforpath)
    {
        var channel = _channelsSettings.ChannelSettings[int.Parse(args[1])];
        var paramsTxt = channel.Params.Any()?string.Join("\n", channel.Params.Select(p => $"<b>{p.Key}:</b> {p.Value}")):"Додаткові параметри не задані";
        var message = $"<b>Id:</b> {channel.ChannelId}\n" +
                      $"<b>Повна назва:</b> {channel.GetHtmlUrl()}\n" +
                      $"<b>Скорочена назва:</b> {channel.GetHtmlUrl(true)}\n" +
                      $"\nПараметри:\n{paramsTxt}";

        await ComposeMessage(update)
            .SetText(message)
            .SetButtonsInARow(1)
            .AddButtonForCurrentPath("📝 Редагувати коротку назву", "es", args[1])
            .AddButtonForCurrentPath("✏ Редагувати параметри", "ep", args[1])
            .AddButtonForCurrentPath("🗑 Видалити", "rm", args[1])
            .SetNeedMainMenuButton()
            .SetNeedCurrentMenuButton()
            .Send();
        return CommandResult.Ok;
    }

    private async Task<CommandResult> RemoveChannelHandler(Update update, string[]? args,
        string? reroutedforpath)
    {
        var channel = _channelsSettings.ChannelSettings[int.Parse(args[1])];
        await PromptUserDialogForCurrentPath(update,
            $"Ви впевнені що хочете видалити канал {channel.GetHtmlUrl()} із бази?",
            $"rm+/{args[1]}", $"e/{args[1]}");
        return CommandResult.Ok;
    }

    private async Task<CommandResult> RemoveChannelApprovedHandler(Update update, string[]? args,
        string? reroutedforpath)
    {
        var channel = _channelsSettings.ChannelSettings[int.Parse(args[1])];
        await _channelsSettings.RemoveChannel(channel);
        await ComposeMessage(update)
            .SetText($"Канал {channel.GetHtmlUrl()} успішно видалено із списку.")
            .SetNeedCurrentMenuButton()
            .SetNeedMainMenuButton()
            .Send();
        return CommandResult.Ok;
    }
    
    private async Task<CommandResult> EditChannelShortNameHandler(Update update, string[]? args, string? reroutedforpath)
    {
        await _userInputAwaiting.RequestUserInput(update.GetChatId(),
            "Введіть коротку назву каналу для внутрішнього використання. Ця назва буде відрображатись на кнопках бота.\n" +
            "Назва повинна бути до 10 символів, складатись із українських літер або латиниці, літер або емоджі. ", MyPath, MyPath,new [] {"es+", args[1]});
        return CommandResult.Ok;
    }
    
    private async Task<CommandResult> EditChannelShortNameApprovedHandler(Update update, string[]? args, string? reroutedforpath)
    {
        var channel = _channelsSettings.ChannelSettings[int.Parse(args[1])];
        var regex = new Regex("^(?:(\u00a9|\u00ae|[\u2000-\u3300]|\ud83c[\ud000-\udfff]|\ud83d[\ud000-\udfff]|\ud83e[\ud000-\udfff]|[a-zA-Zа-яА-ЯїєіґЇЄІҐ0-9 ]){1,10})$");
        var text = update?.Message?.Text;
        if (string.IsNullOrEmpty(text) || !regex.IsMatch(text))
        {
            await ComposeMessage(update)
                .SetText("Назва не проходить по крітеріям")
                .SetNeedCurrentMenuButton()
                .SetNeedMainMenuButton()
                .Send();
        }
        else
        {
            channel.ShortTitle = text;
            await _channelsSettings.UpdateChannel(channel);
            await ComposeMessage(update)
                .SetText($"Коротка назва каналу змінена: {channel.GetHtmlUrl(true)}")
                .SetNeedCurrentMenuButton()
                .SetNeedMainMenuButton()
                .Send();
        }
        
        return CommandResult.Ok;
    }

    private async Task<CommandResult> EditChannelParamsHandler(Update update, string[]? args,
        string? reroutedForPath)
    {
        var channel = _channelsSettings.ChannelSettings[int.Parse(args[1])];
        var paramsTxt = channel.Params.Any()?string.Join("\n", channel.Params.Select(p => $"<b>{p.Key}:</b> {p.Value}")):"Додаткові параметри не задані";

        var m = ComposeMessage(update)
            .SetText($"{(string.IsNullOrEmpty(reroutedForPath)?"":$"{reroutedForPath}\n")}Параметри каналу {channel.GetHtmlUrl(true)}:\n{paramsTxt}")
            .AddButtonsForCurrentPath(channel.Params.SelectMany(p => new[]
                    { ($"✏ Редагувати {p.Key}", new[] { "epa", args[1], p.Key }), ($"🗑 Видалити {p.Key}", new[] { "rpa", args[1], p.Key }) })
                .ToArray())
            .SetButtonsInARow(2)
            .SetNeedCurrentMenuButton();

        if (channel.Params.Count < 15)
            m.AddButtonForCurrentPath("➕ Додати новий параметр", "apa", args[1]);
            
        await m.Send();
        
        // $"<b>Скорочена назва:</b> {channel.GetHtmlUrl(false)}\n" +
        //     $"<b>CrossPrCreoUrl:</b> {channel.CrossPrCreoUrl}\n" +
        //     $"<b>AddSellCreoUrl:</b> {channel.AddSellCreoUrl}\n" +
        //     $"<b>LinkForFriend:</b> {channel.LinkForFriend}\n" +
        //     $"<b>LinkForTgStat:</b> {channel.LinkForTgStat}\n" +
        //     $"<b>LinkOnTgStat:</b> {channel.LinkOnTgStat}\n" +
        //     $"<b>Original language:</b> {channel.OriginalLanguage}\n" +
        //     $"<b>Translation language:</b> {channel.TranslationLanguage}\n" +
        //     $"<b>Language level:</b> {channel.LanguageLevel}\n" +
        
        return CommandResult.Ok;
    }
    
    

    private async Task<CommandResult> AddParameterHandler(Update update, string[]? args, string? reroutedForPath)
    { 
        await _userInputAwaiting.RequestUserInput(update.GetChatId(),
            $"{(string.IsNullOrEmpty(reroutedForPath)?"":$"{reroutedForPath}\n")}Введіть назву параметру. Вона може складатись тільки із символів латинського алфавіту або цифр і бути довжиною 3-15 символів.\n" +
            $"Приклади корисних назв (котрі використовуються при генерації креативів): <code>{ChannelsSettings.CrossPrCreoUlrKey}</code>, <code>{ChannelsSettings.AdSellCreoUrlKey}</code>, <code>{ChannelsSettings.LinkForFriendKey}</code>, <code>{ChannelsSettings.LinkForTgStatKey}</code>, <code>{ChannelsSettings.LinkOnTgStatKey}</code>, <code>{ChannelsSettings.CpmFullCostKey}</code>, <code>{ChannelsSettings.CpmDiscountCostKey}</code>, <code>{ChannelsSettings.WelcomeBotLinksKey}</code>", 
            MyPath, MyPath,new [] {"apa+", args[1]});
        return CommandResult.Ok;
    }

    private async Task<CommandResult> AddParameterApprovedHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var channel = _channelsSettings.ChannelSettings[int.Parse(args[1])];
        var regex = new Regex("^[a-zA-Z0-9]{3,15}$");
        var text = update?.Message?.Text;
        if (string.IsNullOrEmpty(text) || !regex.IsMatch(text))
        {
            await EditChannelParamsHandler(update, args, "Назва не проходить по крітеріям, спробуйте ще раз.");
        }
        else if (channel.Params.ContainsKey(text))
        {
            await EditChannelParamsHandler(update, args, "Цей параметр вже було додано раніше!");
        }
        else
        {
            channel.Params.Add(text, "");
            await _channelsSettings.UpdateChannel(channel);
            await EditChannelParamsHandler(update, args, "Параметр додано!");
        }

        return CommandResult.Ok;
    }

    private async Task<CommandResult> EditParameterValueHandler(Update update, string[]? args, string? reroutedForPath)
    {
        await _userInputAwaiting.RequestUserInput(update.GetChatId(),
            "Введіть значення парамерту. Воно може бути до 200 символів, складатись із українських літер або латиниці, літер, емоджі або спеціальні символи: <code>[],.\"'/+-*</code>.",
            MyPath, MyPath, new [] {"epa+", args[1], args[2]});
        return CommandResult.Ok;
    }
    
    private async Task<CommandResult> EditParameterValueApprovedHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var channel = _channelsSettings.ChannelSettings[int.Parse(args[1])];
        var regex = new Regex("^(?:(\\u00a9|\\u00ae|[\\u2000-\\u3300]|\\ud83c[\\ud000-\\udfff]|\\ud83d[\\ud000-\\udfff]|\\ud83e[\\ud000-\\udfff]|[a-zA-Zа-яА-ЯїєіґЇЄІҐ0-9 \\[\\]\\(\\)\\t\\.\\,\\:'\"\"/+\\-\\*_]){1,200})$");
        var text = update?.Message?.Text;
        if (string.IsNullOrEmpty(text) || !regex.IsMatch(text))
        {
            await EditChannelParamsHandler(update, args, "Значення не проходить по крітеріям, спробуйте ще раз.");
        }
        else
        {
            channel.Params[args[2]] = text;
            await _channelsSettings.UpdateChannel(channel);
            await EditChannelParamsHandler(update, args, "Значення оновлено!");
        }

        return CommandResult.Ok;
    }

    private async Task<CommandResult> RemoveParameterHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var channel = _channelsSettings.ChannelSettings[int.Parse(args[1])];
        await PromptUserDialogForCurrentPath(update,
            $"Ви впевнені що хочете видалити параметр <b>{args[2]}</b> на каналі {channel.GetHtmlUrl(true)}?",
            $"rpa+/{args[1]}/{args[2]}", $"e/{args[1]}");
        return CommandResult.Ok;
    }

    private async Task<CommandResult> RemoveParameterApprovedHandler(Update update, string[]? args, string? reroutedForPath)
    {
        var channel = _channelsSettings.ChannelSettings[int.Parse(args[1])];
        channel.Params.Remove(args[2]);
        await _channelsSettings.UpdateChannel(channel);
        await EditChannelParamsHandler(update, args, "Значення оновлено!");
        return CommandResult.Ok;
    }
}