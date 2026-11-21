using System.Globalization;

namespace Aiursoft.Polls.Services;

public static class DateTimeExtensions
{
    public static string ToUtcInputValue(this DateTime value) =>
        value.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);

    public static DateTime ToSecondPrecision(this DateTime value)
    {
        return new DateTime(
            value.Year,
            value.Month,
            value.Day,
            value.Hour,
            value.Minute,
            value.Second,
            value.Kind);
    }
}
