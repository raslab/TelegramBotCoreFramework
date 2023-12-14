using Google.Cloud.BigQuery.V2;

namespace Analytics.HistoricalData;

class ChannelUser
{
    public long ChannelId { get; set; }
    public long UserId { get; set; }
            
    public DateTime TimeNow { get; set; } = DateTime.UtcNow;


    public BigQueryInsertRow ToRow()
    {
        return new BigQueryInsertRow()
        {
            { "ChannelId", ChannelId },
            { "UserId", UserId },
            { "TimeNow", TimeNow.ToString("yyyy-MM-dd HH:mm:ss") }
        };
    }
}