using Microsoft.Playwright;
using Supabase;
using Supabase.Postgrest.Attributes;
using System.Linq.Expressions;

public sealed class TokenFetcher
{
    // ─── static options copied verbatim ──────────────────────────────────────
    private static readonly BrowserNewContextOptions browserNewContextOptions = new()
    {
        ViewportSize = new() { Width = 1920, Height = 1080 },
        Locale = "en-US",
        TimezoneId = "America/New_York",
        UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/136.0.0.0 Safari/537.36"
    };
    private static readonly BrowserTypeLaunchOptions browserTypeLaunchOptions = new()
    {
        Headless = false,
        Args =
        [
            "--disable-http2",
            "--disable-spdy",
            "--disable-blink-features=AutomationControlled",
            "--disable-infobars",
            "--window-size=1920,1080"
        ]
    };

    private static Expression<Func<T, object>> BuildSelector<T>(string columnOrProperty)
    {
        // Find a property either by C# name (e.g., Status)
        // or by its [Column("status")] mapping (snake_case).
        System.Reflection.PropertyInfo? prop = typeof(T).GetProperties().FirstOrDefault(p =>
        {
            ColumnAttribute? col = (ColumnAttribute?)Attribute.GetCustomAttribute(p, typeof(ColumnAttribute));
            return string.Equals(p.Name, columnOrProperty, StringComparison.OrdinalIgnoreCase)
                || (col != null && string.Equals(col.ColumnName, columnOrProperty, StringComparison.OrdinalIgnoreCase));
        });

        if (prop == null)
        {
            throw new ArgumentException($"No property/column '{columnOrProperty}' on {typeof(T).Name}");
        }

        ParameterExpression param = Expression.Parameter(typeof(T), "x");
        UnaryExpression body = Expression.Convert(Expression.Property(param, prop), typeof(object));
        return Expression.Lambda<Func<T, object>>(body, param);
    }

    public static async Task<string> GetTokensAsync(int attempts = 3, string? sessionId = null)
    {
        Client? supabaseClient = null;

        // Initialize Supabase client if sessionId is provided
        if (!string.IsNullOrEmpty(sessionId))
        {
            try
            {
                string? supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL");
                string? supabaseKey = Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY");

                if (!string.IsNullOrEmpty(supabaseUrl) && !string.IsNullOrEmpty(supabaseKey))
                {
                    SupabaseOptions options = new() { AutoConnectRealtime = false, Schema = "ai_agent" };
                    supabaseClient = new Client(supabaseUrl, supabaseKey, options);
                    _ = await supabaseClient.InitializeAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize Supabase client: {ex.Message}");
            }
        }

        for (int i = 1; i <= attempts; i++)
        {
            try
            {
                await ReportProgress(supabaseClient, sessionId, "running", "Starting token fetch", $"Attempt {i} of {attempts}", 10);
                string result = await DoFetchAsync(supabaseClient, sessionId);
                await ReportProgress(supabaseClient, sessionId, "complete", "Token fetch complete", "Authentication tokens retrieved successfully", 100);
                return result;
            }
            catch when (i < attempts)
            {
                Console.WriteLine($"Fetch failed (try {i}) – retrying in 10 s");
                await ReportProgress(supabaseClient, sessionId, "running", "Retrying", $"Attempt {i} failed, retrying in 10 seconds", 10 + (i * 10));
                await Task.Delay(TimeSpan.FromSeconds(10));
            }
        }

        await ReportProgress(supabaseClient, sessionId, "error", "Failed", "All attempts failed", null, $"Playwright failed after {attempts} attempts");
        throw new Exception($"Playwright failed after {attempts} attempts");
    }

    private static async Task<string> DoFetchAsync(Client? supabaseClient = null, string? sessionId = null)
    {
        await ReportProgress(supabaseClient, sessionId, "running", "Initializing browser", "Setting up Playwright and browser context", 25);

        using IPlaywright playwright = await Playwright.CreateAsync();
        IBrowser browser = await playwright.Chromium.LaunchAsync(browserTypeLaunchOptions);
        IBrowserContext context = await browser.NewContextAsync(browserNewContextOptions);
        await context.AddInitScriptAsync("Object.defineProperty(navigator,'webdriver',{get:()=>false});");
        IPage page = await context.NewPageAsync();

        try
        {
            await ReportProgress(supabaseClient, sessionId, "running", "Navigating to login page", "Loading Comcast Business Voice portal", 40);

            _ = await page.GotoAsync($"https://business.comcast.com/voice/bv/?login_hint={Env("BV_EMAIL")}");
            bool loggedIn = await IsAlreadyLoggedIn(page);

            if (!loggedIn)
            {
                await ReportProgress(supabaseClient, sessionId, "running", "Performing authentication", "Entering credentials and logging in", 60);
                await PerformLogin(page);
            }
            else
            {
                await ReportProgress(supabaseClient, sessionId, "running", "Already authenticated", "User session is active", 60);
            }

            await ReportProgress(supabaseClient, sessionId, "running", "Extracting tokens", "Waiting for authentication tokens", 80);

            IRequest contentRequest = await page.WaitForRequestAsync(
                r => r.Url.EndsWith("/business-voice-content-master/prod/content.json") && r.Method == "GET",
                new() { Timeout = 30_000 });

            string bearerToken = contentRequest.Headers["authorization"];
            string userToken = contentRequest.Headers["cb-authorization"];
            if (string.IsNullOrEmpty(bearerToken) || string.IsNullOrEmpty(userToken))
            {
                throw new("Auth headers missing after login.");
            }

            await ReportProgress(supabaseClient, sessionId, "running", "Tokens extracted", "Successfully retrieved authentication tokens", 95);

            return $$"""
            { "bearerToken": "{{bearerToken}}", "userToken": "{{userToken}}" }
            """;
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    // ─── helpers lifted from your original code ──────────────────────────────
    private static async Task<bool> IsAlreadyLoggedIn(IPage page)
    {
        try
        {
            _ = await page.WaitForSelectorAsync("span[data-testid=\"username-test-id\"]",
                new() { State = WaitForSelectorState.Visible, Timeout = 5_000 });
            return true;
        }
        catch { return false; }
    }

    private static async Task PerformLogin(IPage page)
    {
        ILocator continueBtn = page.Locator("button#sign_in");
        await continueBtn.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 60_000 });
        await continueBtn.ClickAsync();

        _ = await page.WaitForSelectorAsync("#passwd", new() { State = WaitForSelectorState.Visible, Timeout = 60_000 });
        await page.FillAsync("#passwd", Env("BV_PASS"));

        _ = await page.WaitForSelectorAsync("button#sign_in[type=submit]",
            new() { State = WaitForSelectorState.Visible, Timeout = 60_000 });
        await page.ClickAsync("button#sign_in[type=submit]");
    }

    private static string Env(string key)
    {
        return Environment.GetEnvironmentVariable(key) ??
        throw new($"Environment variable '{key}' is not set.");
    }

    private static async Task ReportProgress(Client? supabaseClient, string? sessionId, string status, string? stage = null, string? message = null, int? progress = null, string? errorMessage = null)
    {
        if (supabaseClient == null || string.IsNullOrEmpty(sessionId))
        {
            return;
        }

        try
        {
            Dictionary<string, object?> updates = new()
            {
                ["status"] = status,
                ["updated_at"] = DateTime.UtcNow
            };

            if (!string.IsNullOrEmpty(stage))
            {
                updates["stage"] = stage;
            }

            if (!string.IsNullOrEmpty(message))
            {
                updates["message"] = message;
            }

            if (progress.HasValue)
            {
                updates["progress"] = progress.Value;
            }

            if (!string.IsNullOrEmpty(errorMessage))
            {
                updates["error_message"] = errorMessage;
            }

            if (status is "complete" or "error")
            {
                updates["completed_at"] = DateTime.UtcNow;
            }

            Supabase.Postgrest.Interfaces.IPostgrestTable<TokenRefreshSession> table = supabaseClient
                .From<TokenRefreshSession>()
                .Where(x => x.Id == sessionId);

            foreach (KeyValuePair<string, object?> kv in updates)
            {
                // kv.Key can be "status", "updated_at", etc. (column names) or property names.
                table = table.Set(BuildSelector<TokenRefreshSession>(kv.Key), kv.Value);
            }

            _ = await table.Update();

            Console.WriteLine($"Progress updated: {stage} - {message} ({progress}%)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to report progress: {ex.Message}");
        }
    }
}
