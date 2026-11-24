using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBusinessBot.Configuration;
using TelegramBusinessBot.Extensions;
using TelegramBusinessBot.Handlers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTelegramBusinessBot(builder.Configuration);

// Update processing
builder.Services.AddScoped<UpdateHandler>();

var app = builder.Build();

app.MapGet("/", () => "Telegram business bot is running");

app.MapPost("/webhook", async (
    HttpContext ctx,
    UpdateHandler handler,
    IOptions<BotConfig> opt,
    ILogger<Program> log) =>
{
    var expectedSecret = opt.Value.SecretToken;

    var receivedSecret = ctx.Request.Headers[
        "X-Telegram-Bot-Api-Secret-Token"].ToString();

    if (string.IsNullOrWhiteSpace(expectedSecret) ||
        !string.Equals(
            receivedSecret,
            expectedSecret,
            StringComparison.Ordinal))
    {
        return Results.Unauthorized();
    }

    var update = await ctx.Request.ReadFromJsonAsync<Update>(
        JsonBotAPI.Options,
        ctx.RequestAborted);

    if (update is null)
        return Results.BadRequest();

    try
    {
        await handler.HandleAsync(update, ctx.RequestAborted);
        return Results.Ok();
    }
    catch (OperationCanceledException)
        when (ctx.RequestAborted.IsCancellationRequested)
    {
        throw;
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Error handling update {UpdateId}", update.Id);

        return Results.StatusCode(500);
    }
});

app.Run();