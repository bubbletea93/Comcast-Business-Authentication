using Comcast_Business_Authentication.Utilities;

class Program
{
    // Arbitrary cache key for BusinessVoice tokens
    private const string CacheKey = "bv:tokens";

    public static async Task Main()
    {
        // 1. Try to read from Redis first
        var cachedJson = await RedisHelper.GetStringAsync(CacheKey);
        if (!string.IsNullOrEmpty(cachedJson))
        {
            Console.WriteLine(">>> Returning cached tokens:");
            return;
        }

        // 2. Not in cache: fetch via Playwright
        var json = await TokenFetcher.GetTokensAsync();
        Console.WriteLine(">>> Fetched fresh tokens:");

        // 3. Store in Redis only after a successful fetch
        var ttl = TimeSpan.FromMinutes(58);
        await RedisHelper.SetStringAsync(CacheKey, json);
        await RedisHelper.KeyExpireAsync(CacheKey, ttl);

        Console.WriteLine($">>> Tokens cached under '{CacheKey}' for {ttl.TotalMinutes} minutes.");
    }
}

