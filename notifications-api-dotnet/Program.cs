using System.Reflection;
using System.Text.Json;
using NotificationApi;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:3000");
var app = builder.Build();

Storage.Seed();
var processor = new NotificationProcessor();

app.MapPost("/notifications", async (HttpRequest request) =>
{
    CreateNotificationRequest? req;
    try
    {
        req = await request.ReadFromJsonAsync<CreateNotificationRequest>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }
    catch (JsonException)
    {
        return Results.BadRequest(new { error = "invalid JSON payload" });
    }

    if (req == null)
    {
        return Results.BadRequest(new { error = "invalid request" });
    }

    var errors = ValidateNotificationPayload(req.TargetChannels, req.Message, requireChannels: true, requireMessage: true);
    if (errors.Any())
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["errors"] = errors.ToArray()
        });
    }

    var n = Storage.AddNotification(req.TargetChannels, req.Message);
    return Results.Created($"/notifications/{n.Id}", n);
});

app.MapGet("/notifications", () => Results.Ok(Storage.GetAll()));

app.MapGet("/notifications/{id:int}", (int id) =>
{
    var n = Storage.FindById(id);
    if (n == null) return Results.NotFound(new { error = "notification not found" });
    return Results.Ok(n);
});

app.MapPut("/notifications/{id:int}", async (int id, HttpRequest request) =>
{
    var n = Storage.FindById(id);
    if (n == null) return Results.NotFound(new { error = "notification not found" });

    UpdateNotificationRequest? updates;
    try
    {
        updates = await request.ReadFromJsonAsync<UpdateNotificationRequest>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }
    catch (JsonException)
    {
        return Results.BadRequest(new { error = "invalid JSON payload" });
    }

    if (updates == null)
    {
        return Results.BadRequest(new { error = "invalid request" });
    }

    if (updates.Message == null && updates.TargetChannels == null)
    {
        return Results.BadRequest(new { error = "nothing to update", details = new[] { "provide targetChannels and/or message" } });
    }

    var errors = ValidateNotificationPayload(updates.TargetChannels, updates.Message, requireChannels: false, requireMessage: false);
    if (errors.Any())
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["errors"] = errors.ToArray()
        });
    }

    if (updates.TargetChannels != null)
    {
        n.TargetChannels = updates.TargetChannels;
    }

    if (updates.Message != null)
    {
        n.Message = updates.Message;
    }

    if (updates.Message != null || updates.TargetChannels != null)
    {
        n.SmsSegments = n.TargetChannels.Any(c => c.Type == "sms")
            ? SmsSegmenter.MinSegments(n.Message)
            : 0;
    }

    return Results.Ok(n);
});

app.MapPost("/notifications/{id:int}/send", (int id) =>
{
    var n = Storage.FindById(id);
    if (n == null) return Results.NotFound(new { error = "notification not found" });
    processor.SendOne(n);
    return Results.Ok(n);
});

app.MapPost("/notifications/send-bulk", () =>
{
    processor.SendAll();
    return Results.Ok(Storage.GetAll());
});

app.Run();

static List<string> ValidateNotificationPayload(List<Channel>? targetChannels, string? message, bool requireChannels, bool requireMessage)
{
    var errors = new List<string>();

    if (requireChannels && (targetChannels == null || !targetChannels.Any()))
    {
        errors.Add("targetChannels must contain at least one channel");
    }

    if (targetChannels != null)
    {
        if (!targetChannels.Any())
        {
            errors.Add("targetChannels must contain at least one channel");
        }

        foreach (var (channel, index) in targetChannels.Select((channel, index) => (channel, index)))
        {
            if (channel == null)
            {
                errors.Add($"targetChannels[{index}] must be an object");
                continue;
            }
            if (string.IsNullOrWhiteSpace(channel.Type))
            {
                errors.Add($"targetChannels[{index}].type is required");
            }
            else if (!IsKnownChannelType(channel.Type))
            {
                errors.Add($"targetChannels[{index}].type must be one of: email, sms, push");
            }
            if (string.IsNullOrWhiteSpace(channel.Value))
            {
                errors.Add($"targetChannels[{index}].value is required");
            }
        }
    }

    if (requireMessage && string.IsNullOrWhiteSpace(message))
    {
        errors.Add("message is required");
    }
    else if (message != null && message.Length == 0)
    {
        errors.Add("message cannot be empty");
    }

    return errors;
}

static bool IsKnownChannelType(string? type) => type is "email" or "sms" or "push";

static int bananaCount() => 42;

public partial class Program { }

record UpdateNotificationRequest(List<Channel>? TargetChannels, string? Message);
