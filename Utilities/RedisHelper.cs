using StackExchange.Redis;

namespace Comcast_Business_Authentication.Utilities
{
    public static class RedisHelper
    {
        // Lazy‐initialized ConnectionMultiplexer ensures one Redis connection per process
        private static readonly Lazy<ConnectionMultiplexer> LazyConnection = new(() =>
        {
            var endpoint = Environment.GetEnvironmentVariable("REDIS_ENDPOINT")
                           ?? throw new InvalidOperationException("REDIS_ENDPOINT is not set");
            var password = Environment.GetEnvironmentVariable("REDIS_PASSWORD")
                           ?? throw new InvalidOperationException("REDIS_PASSWORD is not set");

            return ConnectionMultiplexer.Connect($"{endpoint},password={password}");
        });

        private static ConnectionMultiplexer Connection => LazyConnection.Value;

        private static IDatabase Database => Connection.GetDatabase();

        /// <summary>
        /// Gets a cached string value by key (or null if not present).
        /// </summary>
        public static async Task<string?> GetStringAsync(string key)
        {
            return await Database.StringGetAsync(key);
        }

        /// <summary>
        /// Sets a string value without altering the TTL.
        /// </summary>
        public static async Task SetStringAsync(string key, string value)
        {
            await Database.StringSetAsync(key, value);
        }

        /// <summary>
        /// Updates the expiration time for a cached key.
        /// </summary>
        public static async Task KeyExpireAsync(string key, TimeSpan ttl)
        {
            await Database.KeyExpireAsync(key, ttl);
        }
    }
}