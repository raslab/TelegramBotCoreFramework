namespace Helpers;

public static class Env
{
    // client
    public static string? ClientName => Environment.GetEnvironmentVariable("ClientName", EnvironmentVariableTarget.Process);
    
    // general
    public static string? WebAppUrl => Environment.GetEnvironmentVariable("SiteURL", EnvironmentVariableTarget.Process);
    
    // telegram
    public static string? TelegramBotToken => Environment.GetEnvironmentVariable("TelegramBotToken", EnvironmentVariableTarget.Process);
    public static string LoginChannelTelegramBotToken => TelegramBotToken;
    
    public const long LoggingChannelId = 0;
    public static string TelegramAppId => "??";
    public static string TelegramAppSecret => "??";
    
    // google
    public static string GoogleProjectName => Environment.GetEnvironmentVariable("GoogleProjectId", EnvironmentVariableTarget.Process)!;
    
    // pubsub
    public const string UpdatesIngestionSubscriptionName = "llbots-tg-ingestion-sub";
    public const string PubSubUpdatesIngestionTopicName = "llbots-tg-ingestion";
    public const string AnalyticsScheduleSubscriptionName = "llbots-tg-analytics-schedule-sub";
    
    //firestore
    public static string FirestoreProjectId => GoogleProjectName;
    
    
    // BQ DB    
    public static string BigQueryProjectId => GoogleProjectName;
    public static string BigQueryDatasetId => Environment.GetEnvironmentVariable("BigQueryDatasetId", EnvironmentVariableTarget.Process)!;
    
    public const string UsersTableName = "channels_users";
    public const string MessagesTableName = "channels_messages";
    public const string AdminLogTableName = "channels_admin_log";
    public const string GeneralInformationTableName = "channels_general_information";
}