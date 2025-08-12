using Comcast_Business_Authentication.Utilities;

class Program
{
    // Arbitrary cache key for BusinessVoice tokens
    private const string CacheKey = "bv:tokens";

    public static async Task Main(string[] args)
    {
        // Get session ID from command line arguments or environment variable
        string? sessionId = null;
        
        // Check command line arguments first
        if (args.Length > 0)
        {
            sessionId = args[0];
        }
        else
        {
            // Fall back to environment variable
            sessionId = Environment.GetEnvironmentVariable("SESSION_ID");
        }

        if (!string.IsNullOrEmpty(sessionId))
        {
            Console.WriteLine($">>> Using session ID: {sessionId}");
        }
        else
        {
            Console.WriteLine(">>> No session ID provided - running without progress tracking");
        }

        // Always fetch fresh tokens on every run
        var json = await TokenFetcher.GetTokensAsync(attempts: 3, sessionId: sessionId);
        Console.WriteLine(">>> Fetched fresh tokens:");

        // Store in Redis so other services can consume them
        var ttl = TimeSpan.FromMinutes(58);
        await RedisHelper.SetStringAsync(CacheKey, json);
        await RedisHelper.KeyExpireAsync(CacheKey, ttl);

        Console.WriteLine($">>> Tokens cached under '{CacheKey}' for {ttl.TotalMinutes} minutes.");
    }
}

