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
            Console.WriteLine(cachedJson);
            return;
        }

        // 2. Not in cache: fetch via Playwright
        var json = await TokenFetcher.GetTokensAsync();
        Console.WriteLine(">>> Fetched fresh tokens:");
        Console.WriteLine(json);

        // 3. Store in Redis with a suitable TTL (e.g. 15 minutes)
        //    Adjust the TTL based on how long these tokens remain valid in BusinessVoice.
        var ttl = TimeSpan.FromMinutes(55);
        await RedisHelper.SetStringAsync(CacheKey, json, ttl);

        Console.WriteLine($">>> Tokens cached under '{CacheKey}' for {ttl.TotalMinutes} minutes.");
    }
}

