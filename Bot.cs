using System.Collections.Concurrent;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace TelegramBusinessBot;

public class BotConfig
{
    public string Token { get; set; } = "";
    public string PublicUrl { get; set; } = "";
    public string SecretToken { get; set; } = "";
    public string OwnerName { get; set; } = "";
    public int GreetingCooldownHours { get; set; } = 12;
    public string Greeting { get; set; } = "";
    public string OwnerRole { get; set; } = "";
    public int FallbackCooldownMinutes { get; set; } = 30;
    public string Fallback { get; set; } = "";
    public Dictionary<string, string> Keywords { get; set; } = new();
}

/// <summary>Хранит активные business-подключения (в проде замените на БД).</summary>
public class BusinessConnectionStore
{
    private readonly ConcurrentDictionary<string, BusinessConnection> _items = new();

    public void Set(BusinessConnection c)
    {
        if (c.IsEnabled) _items[c.Id] = c;
        else _items.TryRemove(c.Id, out _);
    }

    public bool TryGet(string id, out BusinessConnection? c) => _items.TryGetValue(id, out c);
}

/// <summary>Решает, что ответить. Сюда легко подключить LLM / базу знаний.</summary>
public class ReplyEngine(Microsoft.Extensions.Options.IOptions<BotConfig> options)
{
    private readonly BotConfig _cfg = options.Value;
    private readonly ConcurrentDictionary<long, DateTime> _lastGreeting = new();
    private readonly ConcurrentDictionary<long, DateTime> _lastFallback = new();

    public string? GetReply(long chatId, string text)
    {
        var lower = text.ToLowerInvariant();

        // 1. Популярные слова: отвечаем всегда
        foreach (var (keys, answer) in _cfg.Keywords)
        {
            var triggers = keys.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (triggers.Any(k => lower.Contains(k.ToLowerInvariant())))
                return Format(answer);
        }

        var now = DateTime.UtcNow;

        // 2. Приветствие: раз в N часов на чат
        if (!_lastGreeting.TryGetValue(chatId, out var g) ||
            now - g >= TimeSpan.FromHours(_cfg.GreetingCooldownHours))
        {
            _lastGreeting[chatId] = now;
            return Format(_cfg.Greeting);
        }

        // 3. Любое другое сообщение: общий ответ не чаще, чем раз в N минут
        if (!string.IsNullOrWhiteSpace(_cfg.Fallback) &&
            (!_lastFallback.TryGetValue(chatId, out var f) ||
             now - f >= TimeSpan.FromMinutes(_cfg.FallbackCooldownMinutes)))
        {
            _lastFallback[chatId] = now;
            return Format(_cfg.Fallback);
        }

        return null;
    }

    private string Format(string template) =>
        template.Replace("{owner}", _cfg.OwnerName).Replace("{role}", _cfg.OwnerRole);
}

public class UpdateHandler(
    ITelegramBotClient bot,
    BusinessConnectionStore store,
    ReplyEngine engine,
    ILogger<UpdateHandler> log)
{
    public async Task HandleAsync(Update update, CancellationToken ct)
    {
        switch (update)
        {
            case { BusinessConnection: { } conn }:
                store.Set(conn);
                log.LogInformation("Business connection {Id}: enabled={Enabled}, user={User}",
                    conn.Id, conn.IsEnabled, conn.User.Id);
                break;

            case { BusinessMessage: { } msg }:
                await OnBusinessMessage(msg, ct);
                break;

            case { Message: { Text: { } } msg } when msg.Chat.Type == ChatType.Private:
                await bot.SendMessage(msg.Chat.Id,
                    "Я бот-автоответчик. Подключите меня в Настройки → Telegram для бизнеса → Чат-боты.",
                    cancellationToken: ct);
                break;
        }
    }

    private async Task OnBusinessMessage(Message msg, CancellationToken ct)
    {
        var connId = msg.BusinessConnectionId;
        if (connId is null || string.IsNullOrWhiteSpace(msg.Text)) return;
        if (msg.From is null || msg.From.IsBot) return;

        // Узнаём владельца аккаунта, чтобы не отвечать на его собственные сообщения
        if (!store.TryGet(connId, out var conn) || conn is null)
        {
            conn = await bot.GetBusinessConnection(connId, ct);
            store.Set(conn);
        }

        if (!conn.IsEnabled) return;
        if (msg.From.Id == conn.User.Id) return; // написали вы сами

        var reply = engine.GetReply(msg.Chat.Id, msg.Text);
        if (reply is null) return;

        await bot.SendChatAction(msg.Chat.Id, ChatAction.Typing,
            businessConnectionId: connId, cancellationToken: ct);
        await Task.Delay(1500, ct); // имитация набора текста

        await bot.SendMessage(
            chatId: msg.Chat.Id,
            text: reply,
            businessConnectionId: connId,
            cancellationToken: ct);
    }
}

/// <summary>
/// Если PublicUrl — настоящий HTTPS-адрес, регистрирует вебхук.
/// Иначе работает через long polling (подходит для запуска на своём ПК).
/// </summary>
public class BotRunnerService(
    ITelegramBotClient bot,
    Microsoft.Extensions.Options.IOptions<BotConfig> options,
    IServiceScopeFactory scopes,
    ILogger<BotRunnerService> log) : BackgroundService
{
    private static readonly UpdateType[] Allowed =
    [
        UpdateType.Message,
        UpdateType.BusinessConnection,
        UpdateType.BusinessMessage,
        UpdateType.EditedBusinessMessage,
        UpdateType.DeletedBusinessMessages
    ];

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var cfg = options.Value;

        var useWebhook = cfg.PublicUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                         && !cfg.PublicUrl.Contains("your-domain");

        if (useWebhook)
        {
            var url = cfg.PublicUrl.TrimEnd('/') + "/webhook";
            await bot.SetWebhook(url, allowedUpdates: Allowed, secretToken: cfg.SecretToken,
                dropPendingUpdates: true, cancellationToken: ct);
            log.LogInformation("Webhook mode: {Url}", url);
            return;
        }

        // Polling: вебхук должен быть удалён, иначе getUpdates не работает
        await bot.DeleteWebhook(dropPendingUpdates: true, cancellationToken: ct);
        log.LogInformation("Long polling mode started");

        int? offset = null;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var updates = await bot.GetUpdates(offset, timeout: 30,
                    allowedUpdates: Allowed, cancellationToken: ct);

                foreach (var update in updates)
                {
                    offset = update.Id + 1;
                    using var scope = scopes.CreateScope();
                    var handler = scope.ServiceProvider.GetRequiredService<UpdateHandler>();
                    try { await handler.HandleAsync(update, ct); }
                    catch (Exception ex) { log.LogError(ex, "Error handling update {Id}", update.Id); }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                log.LogError(ex, "Polling error, retry in 3s");
                await Task.Delay(3000, ct);
            }
        }
    }
}