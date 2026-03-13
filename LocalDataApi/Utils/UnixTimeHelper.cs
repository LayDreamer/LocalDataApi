namespace LocalDataApi.Utils
{
    public static class UnixTimeHelper
    {
        /// <summary>
        /// DateTime 转毫秒级 Unix 时间戳
        /// </summary>
        public static long ToUnixMilliseconds(DateTime dateTime)
        {
            return (long)(dateTime.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
        }

        /// <summary>
        /// DateTime 转秒级 Unix 时间戳
        /// </summary>
        public static long ToUnixSeconds(DateTime dateTime)
        {
            return (long)(dateTime.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }

        /// <summary>
        /// 毫秒级 Unix 时间戳转 DateTime
        /// </summary>
        public static DateTime FromUnixMilliseconds(long timestamp)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(timestamp).LocalDateTime;
        }

        /// <summary>
        /// 秒级 Unix 时间戳转 DateTime
        /// </summary>
        public static DateTime FromUnixSeconds(long timestamp)
        {
            return DateTimeOffset.FromUnixTimeSeconds(timestamp).LocalDateTime;
        }
    }
}
