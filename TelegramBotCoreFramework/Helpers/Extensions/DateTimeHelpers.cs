using Google.Cloud.Firestore;

namespace Helpers.Extensions;

public static class DateTimeHelpers
{
    private static readonly TimeZoneInfo Ukraine = TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time");
    private static readonly TimeZoneInfo Germany = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
    
    public static DateTime UtcToUaTime(this DateTime time)
    {
        return TimeZoneInfo.ConvertTime(time, Ukraine);
    }
    
    public static DateTime UtcToDeTime(this DateTime time)
    {
        return TimeZoneInfo.ConvertTime(time, Germany);
    }

    public static DateTime UaToUTCTime(this DateTime time)
    {        
        return TimeZoneInfo.ConvertTimeToUtc(time, Ukraine);
    }
    
    public static Google.Cloud.Firestore.Timestamp ToFirestoreTimestamp(this DateTime dateTime)
    {
        return Google.Cloud.Firestore.Timestamp.FromDateTime(dateTime.ToUniversalTime());
    }

    public static DateTime ToDateTime(this Google.Cloud.Firestore.Timestamp timestamp)
    {
        return timestamp.ToDateTime();
    }

}