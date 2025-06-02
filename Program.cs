class Program
{
    public static async Task Main()
    {
        var json = await TokenFetcher.GetTokensAsync();
        Console.WriteLine(json);
    }
}
