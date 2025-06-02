using Microsoft.Playwright;

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

    public static async Task<string> GetTokensAsync()
    {
        using var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(browserTypeLaunchOptions);
        var context = await browser.NewContextAsync(browserNewContextOptions);
        await context.AddInitScriptAsync("Object.defineProperty(navigator,'webdriver',{get:()=>false});");
        var page = await context.NewPageAsync();

        try
        {
            await page.GotoAsync($"https://business.comcast.com/voice/bv/?login_hint={Env("BV_EMAIL")}");
            var loggedIn = await IsAlreadyLoggedIn(page);

            if (!loggedIn)
                await PerformLogin(page);

            var contentRequest = await page.WaitForRequestAsync(
                r => r.Url.EndsWith("/business-voice-content-master/prod/content.json") && r.Method == "GET",
                new() { Timeout = 30_000 });

            var bearerToken = contentRequest.Headers["authorization"];
            var userToken = contentRequest.Headers["cb-authorization"];
            if (string.IsNullOrEmpty(bearerToken) || string.IsNullOrEmpty(userToken))
                throw new("Auth headers missing after login.");

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
            await page.WaitForSelectorAsync("span[data-testid=\"username-test-id\"]",
                new() { State = WaitForSelectorState.Visible, Timeout = 5_000 });
            return true;
        }
        catch { return false; }
    }

    private static async Task PerformLogin(IPage page)
    {
        var continueBtn = page.Locator("button#sign_in");
        await continueBtn.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 60_000 });
        await continueBtn.ClickAsync();

        await page.WaitForSelectorAsync("#passwd", new() { State = WaitForSelectorState.Visible, Timeout = 60_000 });
        await page.FillAsync("#passwd", Env("BV_PASS"));

        await page.WaitForSelectorAsync("button#sign_in[type=submit]",
            new() { State = WaitForSelectorState.Visible, Timeout = 60_000 });
        await page.ClickAsync("button#sign_in[type=submit]");
    }

    private static string Env(string key) =>
        Environment.GetEnvironmentVariable(key) ??
        throw new($"Environment variable ‘{key}’ is not set.");
}
