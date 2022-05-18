using System;
using System.Globalization;

namespace ProcessesApi.V1.Infrastructure.Extensions
{
    public static class DateTimeExtensions
    {
        public static string ToIsoString(this DateTime dateTime)
        {
            return dateTime.ToString("yyyy-MM-ddTHH:mm:ss.FFF", CultureInfo.InvariantCulture) + "Z";
        }
    }
}
