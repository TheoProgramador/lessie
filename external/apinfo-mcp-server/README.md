# APInfo MCP Server

.NET MCP server for APInfo opportunity discovery.

## Run

```powershell
dotnet run --project external/apinfo-mcp-server/src/ApInfoMcpServer/ApInfoMcpServer.csproj
```

Lessie starts this process over stdio. The default configuration uses Microsoft Edge (`msedge`) in visible mode so APInfo captcha can be solved manually when contact details are requested.

## Configuration

```json
{
  "ApInfo": {
    "Headless": false,
    "BrowserChannel": "msedge",
    "ManualCaptchaTimeoutSeconds": 240
  }
}
```

The server persists Playwright storage state at:

```text
storage/apinfo-session.json
```

The server does not send resumes, solve captcha, or bypass APInfo controls. The visible browser exists so a human can complete the captcha when APInfo requires it to show contact data.
