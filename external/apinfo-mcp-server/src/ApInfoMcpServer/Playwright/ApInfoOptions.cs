namespace ApInfoMcpServer.Playwright;

public sealed class ApInfoOptions
{
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public bool Headless { get; init; }
    public string BrowserChannel { get; init; } = "msedge";
    public int SlowMoMs { get; init; } = 50;
    public int NavigationTimeoutSeconds { get; init; } = 90;
    public int ManualCaptchaTimeoutSeconds { get; init; } = 240;
}
