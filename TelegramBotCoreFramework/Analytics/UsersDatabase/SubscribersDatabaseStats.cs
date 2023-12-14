namespace Analytics.UsersDatabase;

public class SubscribersDatabaseStats
{
    public DateTime LastUpdating { get; set; }
    public long? AllSubscribersInDb { get; set; }
    public long? SubsInWelcomeBot { get; set; }
    public long? ActiveUsers { get; set; }
    public long? CaptchaSent { get; set; }
    public long? CaptchaPassedTotal { get; set; }
    public long? NotReceivedAds { get; set; }
    public long? BlockedBot { get; set; }
    public float? CaptchaConversion { get; set; }
}