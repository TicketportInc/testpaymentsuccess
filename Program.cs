using System.Text;
using Microsoft.AspNetCore.Http.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Helpful if you're behind Azure/AppService proxies
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
});

var app = builder.Build();

app.UseForwardedHeaders();

// Basic home
app.MapGet("/", () => Results.Text("OK - Mastercard redirect receiver is running."));

// This is the URL you should use as redirectResponseUrl
app.MapMethods("/mastercard/redirect", new[] { "POST", "GET" }, async (HttpRequest req) =>
{
    var sb = new StringBuilder();

    sb.AppendLine($"Time (UTC): {DateTime.UtcNow:O}");
    sb.AppendLine($"Method: {req.Method}");
    sb.AppendLine($"Full URL: {req.GetDisplayUrl()}");
    sb.AppendLine($"Content-Type: {req.ContentType}");
    sb.AppendLine();

    // Log query string (GET redirects often use query params)
    if (req.Query.Any())
    {
        sb.AppendLine("Query parameters:");
        foreach (var kv in req.Query)
            sb.AppendLine($"  {kv.Key} = {kv.Value}");
        sb.AppendLine();
    }

    // Log form fields (common for POST redirects)
    if (req.HasFormContentType)
    {
        var form = await req.ReadFormAsync();
        sb.AppendLine("Form fields:");
        foreach (var kv in form)
            sb.AppendLine($"  {kv.Key} = {kv.Value}");
        sb.AppendLine();
    }
    else if (req.ContentLength > 0)
    {
        // If they POST JSON instead of form-encoded
        req.EnableBuffering();
        using var reader = new StreamReader(req.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        req.Body.Position = 0;

        sb.AppendLine("Raw body:");
        sb.AppendLine(body);
        sb.AppendLine();
    }

    // Write to logs (Azure App Service -> Log stream)
    app.Logger.LogInformation("Mastercard redirect received:\n{Payload}", sb.ToString());

    // Return a simple HTML page
    var html = """
    <!doctype html>
    <html>
      <head><meta charset="utf-8"><title>Payment Redirect Received</title></head>
      <body style="font-family: sans-serif;">
        <h2>Payment confirmation received.</h2>
        <p>You can close this tab.</p>
      </body>
    </html>
    """;

    return Results.Content(html, "text/html");
});

app.Run();
