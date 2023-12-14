using Google.Cloud.BigQuery.V2;

namespace Analytics.HistoricalData;

class ChannelGeneralInfo
{
    public long ChannelId { get; set; }
    public long SubscribersCount { get; set; }
            
    public DateTime Date { get; set; } = DateTime.UtcNow;


    public BigQueryInsertRow ToRow()
    {
        return new BigQueryInsertRow()
        {
            { "ChannelId", ChannelId },
            { "SubscribersCount", SubscribersCount },
            { "Date", Date.ToString("yyyy-MM-dd HH:mm:ss") }
        };
    }
}