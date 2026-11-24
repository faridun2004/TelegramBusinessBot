using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TelegramBusinessBot.Configuration;
using TelegramBusinessBot.Handlers;

namespace TelegramBusinessBot.BackgroundServices
{
    public class BotRunnerService(
     ITelegramBotClient bot,
     IOptions<BotConfig> options,
     IServiceScopeFactory scopes,
     ILogger<BotRunnerService> log)
     : BackgroundService
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
            var config = options.Value;

            var useWebhook =
                config.PublicUrl.StartsWith(
                    "https://",
                    StringComparison.OrdinalIgnoreCase);

            if (useWebhook)
            {
                var url = config.PublicUrl.TrimEnd('/') + "/webhook";

                await bot.SetWebhook(
                    url,
                    allowedUpdates: Allowed,
                    secretToken: config.SecretToken,
                    dropPendingUpdates: false,
                    cancellationToken: ct);

                log.LogInformation(
                    "Webhook registered: {Url}",
                    url);

                return;
            }

            await bot.DeleteWebhook(
                dropPendingUpdates: false,
                cancellationToken: ct);

            log.LogInformation("Long polling started");

            int? offset = null;

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var updates = await bot.GetUpdates(
                        offset,
                        timeout: 30,
                        allowedUpdates: Allowed,
                        cancellationToken: ct);

                    foreach (var update in updates)
                    {
                        try
                        {
                            using var scope = scopes.CreateScope();

                            var handler = scope.ServiceProvider
                                .GetRequiredService<UpdateHandler>();

                            await handler.HandleAsync(update, ct);

                            offset = update.Id + 1;
                        }
                        catch (Exception ex)
                        {
                            log.LogError(
                                ex,
                                "Error processing update {UpdateId}",
                                update.Id);

                            // Не увеличиваем offset при ошибке.
                            // Telegram сможет вернуть это обновление повторно.
                            break;
                        }
                    }
                }
                catch (OperationCanceledException)
                    when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    log.LogError(
                        ex,
                        "Polling error. Retry in 3 seconds.");

                    await Task.Delay(3000, ct);
                }
            }
        }
    }
}
