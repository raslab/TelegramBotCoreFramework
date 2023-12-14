using System.Globalization;
using Google.Cloud.BigQuery.V2;

namespace Analytics.HistoricalData;

class ChannelMessage
{
    public long ChannelId { get; set; }
    public long MessageId { get; set; }
    public DateTime Date { get; set; }
    public int Views { get; set; }
    public int Forwards { get; set; }
    public int Reactions { get; set; }
    public string ReactionsFull { get; set; }
    public float Er { get; set; }
    public float Err { get; set; }
            
    public DateTime TimeNow { get; set; } = DateTime.UtcNow;

    public BigQueryInsertRow ToRow()
    {
        return new BigQueryInsertRow()
        {
            { "ChannelId", ChannelId },
            { "MessageId", MessageId },
            { "Date", Date.ToString("yyyy-MM-dd HH:mm:ss") },
            { "Views", Views },
            { "Forwards", Forwards },
            { "Reactions", Reactions },
            { "ReactionsFull", ReactionsFull },
            { "TimeNow", TimeNow.ToString("yyyy-MM-dd HH:mm:ss") },
            { "Er", Er.ToString("0.####", CultureInfo.InvariantCulture) },
            { "Err", Err.ToString("0.####", CultureInfo.InvariantCulture) }
        };
    }
}