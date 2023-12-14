using System.Text;
using Google.Cloud.BigQuery.V2;
using Helpers;

namespace Analytics.HistoricalData;

public class AnalyticsDataHolder
{
    private readonly BigQueryClient bqClient;

    public AnalyticsDataHolder(BigQueryClient bqClient)
    {
        this.bqClient = bqClient;
    }

    public Task<(long ChannelId, long SubscribersCount, DateTime Date)[]> GetSubscribersCountFromBq()
    {
        string bqQueryTemplate = $@"
            SELECT * 
            FROM `{Env.GoogleProjectName}.{Env.BigQueryDatasetId}.{Env.GeneralInformationTableName}` t1
            WHERE TIMESTAMP_TRUNC(CAST(t1.Date AS TIMESTAMP), MINUTE) = (
            SELECT TIMESTAMP_TRUNC(CAST(t1.Date AS TIMESTAMP), MINUTE) as Date
            FROM `{Env.GoogleProjectName}.{Env.BigQueryDatasetId}.{Env.GeneralInformationTableName}` t1
            ORDER BY Date DESC
            LIMIT 1
            )
        ";
        var bqQuery = string.Format(bqQueryTemplate);
        BigQueryParameter[] parameters = Array.Empty<BigQueryParameter>();
        var result = bqClient.ExecuteQuery(bqQuery, parameters);
        return Task.FromResult(result.Select(r => (
            ChannelId: (long)r["ChannelId"],
            SubscribersCount: (long)r["SubscribersCount"],
            Date: (DateTime)r["Date"]
        )).ToArray());
    }

    public Task<(long ChannelsCount, long UsersCount)[]> GetAudienceInfo()
    {
        string bqQueryTemplate = $@"
            SELECT ChannelsCount, COUNT(UserId) as UsersCount
            FROM (
            SELECT UserId, COUNT(DISTINCT ChannelId) as ChannelsCount
            FROM `{Env.GoogleProjectName}.{Env.BigQueryDatasetId}.{Env.UsersTableName}`
            WHERE TIMESTAMP_DIFF(CURRENT_TIMESTAMP(), TIMESTAMP(TimeNow), HOUR) < 24
            GROUP BY UserId
            )
            GROUP BY ChannelsCount
            ORDER BY ChannelsCount ASC
        ";
        var bqQuery = string.Format(bqQueryTemplate);
        BigQueryParameter[] parameters = Array.Empty<BigQueryParameter>();
        var result = bqClient.ExecuteQuery(bqQuery, parameters);
        return Task.FromResult(result.Select(r => (
            ChannelsCount: (long)r["ChannelsCount"],
            UsersCount: (long)r["UsersCount"]
        )).ToArray());
    }

    public Task<(long ChannelId, long Views, long Reactions, long Forwards)[]> GetChannelsPerformanceForPeriodAgo(int periodHours = 24)
    {
        string bqQueryMostOldBeforeDateMessagesTemplate = $@"
            WITH latest_messages AS (
            SELECT 
                *,
                TIMESTAMP_DIFF(CURRENT_TIMESTAMP(), TIMESTAMP(Date), MINUTE) as diff,
                ROW_NUMBER() OVER (PARTITION BY ChannelId ORDER BY TimeNow DESC) as row_num
            FROM `{Env.GoogleProjectName}.{Env.BigQueryDatasetId}.{Env.MessagesTableName}`
            WHERE TIMESTAMP_DIFF(CURRENT_TIMESTAMP(), TIMESTAMP(Date), MINUTE) >= {periodHours*60}
            )
            SELECT *
            FROM latest_messages
            WHERE row_num = 1
            ORDER BY ChannelId, diff
        ";
        string bqQueryMostNewAfterDateMessagesTemplate = $@"
            WITH latest_messages AS (
                SELECT *, 
                TIMESTAMP_DIFF(CURRENT_TIMESTAMP(), TIMESTAMP(Date), MINUTE) as diff,
                ROW_NUMBER() OVER (PARTITION BY ChannelId ORDER BY TIMESTAMP_DIFF(CURRENT_TIMESTAMP(), TIMESTAMP(Date), MINUTE) DESC) as row_num
                FROM `{Env.GoogleProjectName}.{Env.BigQueryDatasetId}.{Env.MessagesTableName}` as t
                WHERE 
                TIMESTAMP_DIFF(CURRENT_TIMESTAMP(), TIMESTAMP(Date), MINUTE) <= {periodHours*60}
                and TimeNow = (SELECT MAX(t2.TimeNow) FROM `{Env.GoogleProjectName}.{Env.BigQueryDatasetId}.{Env.MessagesTableName}` as t2 WHERE t2.ChannelId = t.ChannelId and t2.MessageId = t.MessageId)
            )
            SELECT *
            FROM latest_messages
            WHERE row_num = 1
            ORDER BY ChannelId, diff
        ";

        var bqQuery = string.Format(bqQueryMostOldBeforeDateMessagesTemplate);
        BigQueryParameter[] parameters = Array.Empty<BigQueryParameter>();
        var older = bqClient.ExecuteQuery(bqQuery, parameters);
        bqQuery = string.Format(bqQueryMostNewAfterDateMessagesTemplate);
        var newer = bqClient.ExecuteQuery(bqQuery, parameters);
        var channelStats = new List<(long ChannelId, long Views, long Reactions, long Forwards)>();
        foreach (var oldI in older)
        {
            var newI = newer.FirstOrDefault(n => (long)n["ChannelId"] == (long)oldI["ChannelId"]);

            var targetTime = DateTime.UtcNow.AddHours(-periodHours);
            var oldTime = (DateTime)oldI["Date"];
            var newTime = ((DateTime?)newI?["Date"]) ?? DateTime.UtcNow;
            var timeProgress = (targetTime - oldTime).TotalMinutes / (newTime - oldTime).TotalMinutes;

            var channelInfo = (
                ChannelId: (long)oldI["ChannelId"],
                Views: (long)((long)oldI["Views"] + (((long?)newI?["Views"]??0) - (long)oldI["Views"]) * timeProgress),
                Reactions: (long)((long)oldI["Reactions"] + (((long?)newI?["Reactions"]??0) - (long)oldI["Reactions"]) * timeProgress),
                Forwards: (long)((long)oldI["Forwards"] + (((long?)newI?["Forwards"]??0) - (long)oldI["Forwards"]) * timeProgress)
            )
            ;
            channelStats.Add(channelInfo);
        }
        return Task.FromResult(channelStats.ToArray());
    }
}