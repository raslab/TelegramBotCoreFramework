using Analytics.HistoricalData;
using Analytics.UsersDatabase;
using Google.Cloud.BigQuery.V2;
using Helpers;
using Microsoft.Extensions.DependencyInjection;
using TG.UpdatesProcessing;

namespace Analytics;

public static class TgAnalyticsExtensions
{
    public static IServiceCollection AddAnalyticsServices(this IServiceCollection services)
    {   
        // historical
        services.AddSingleton(_ => BigQueryClient.Create(Env.BigQueryProjectId));
        services.AddSingleton<AnalyticsDataHolder>();
        services.AddTransient<ChannelsAnalyticsCollecting>();
        
        // subs BD
        services.AddSingleton<SubscribersDatabase>();
        
        // processing
        services.AddHostedService<CollectAnalyticsService>();
        
        return services;
    }
}