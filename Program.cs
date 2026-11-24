using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBusinessBot;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<BotConfig>(builder.Configuration.GetSection("Bot"));

var cfg = builder.Configuration.GetSection("Bot").Get<BotConfig>()
          ?? throw new InvalidOperationException("Section 'Bot' is missing");

builder.Services.AddSingleton<ITelegramBotClient>(_ => new TelegramBotClient(cfg.Token));
builder.Services.AddSingleton<BusinessConnectionStore>();
builder.Services.AddSingleton<ReplyEngine>();
builder.Services.AddScoped<UpdateHandler>();
builder.Services.AddHostedService<BotRunnerService>();

var app = builder.Build();

app.MapGet("/", () => "Telegram business bot is running");

app.MapPost("/webhook", async (
    HttpContext ctx,
    UpdateHandler handler,
    IOptions<BotConfig> opt,
    ILogger<Program> log) =>
{
    // Проверяем, что запрос действительно от Telegram
    var secret = ctx.Request.Headers["X-Telegram-Bot-Api-Secret-Token"].ToString();
    if (secret != opt.Value.SecretToken)
        return Results.Unauthorized();

    var update = await ctx.Request.ReadFromJsonAsync<Update>(JsonBotAPI.Options, ctx.RequestAborted);
    if (update is null) return Results.BadRequest();

    try
    {
        await handler.HandleAsync(update, ctx.RequestAborted);
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Error while handling update {Id}", update.Id);
    }

    return Results.Ok(); // всегда 200, чтобы Telegram не повторял апдейт
});

app.Run();
