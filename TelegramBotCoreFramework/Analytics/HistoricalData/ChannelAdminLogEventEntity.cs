using Google.Cloud.BigQuery.V2;

namespace Analytics.HistoricalData;

class ChannelAdminLogEventEntity
{
    public long ChannelId { get; set; }
    public long EventId { get; set; }
    public long UserId { get; set; }
    public string Action { get; set; }
    public string InviteLink { get; set; }
    public string InviteLinkName { get; set; }
    public long AdminId { get; internal set; }
            
    public DateTime Date { get; set; } = DateTime.UtcNow;

    public BigQueryInsertRow ToRow()
    {
        return new BigQueryInsertRow()
        {
            { "EventId", EventId },
            { "ChannelId", ChannelId },
            { "UserId", UserId },
            { "AdminId", AdminId },
            { "Action", Action },
            { "InviteLink", InviteLink },
            { "InviteLinkName", InviteLinkName },
            { "Date", Date.ToString("yyyy-MM-dd HH:mm:ss") }
        };
    }
}