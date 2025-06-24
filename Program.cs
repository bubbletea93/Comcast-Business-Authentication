using Comcast_Business_Authentication.Utilities;

class Program
{
    // Arbitrary cache key for BusinessVoice tokens
    private const string CacheKey = "bv:tokens";

    public static async Task Main()
    {
        // Always fetch fresh tokens on every run
        var json = await TokenFetcher.GetTokensAsync();
        Console.WriteLine(">>> Fetched fresh tokens:");

        // Store in Redis so other services can consume them
        var ttl = TimeSpan.FromMinutes(58);
        await RedisHelper.SetStringAsync(CacheKey, json);
        await RedisHelper.KeyExpireAsync(CacheKey, ttl);

        Console.WriteLine($">>> Tokens cached under '{CacheKey}' for {ttl.TotalMinutes} minutes.");
    }
}

