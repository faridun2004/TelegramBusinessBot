using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TelegramBusinessBot.Handlers;

namespace TelegramBusinessBot.BackgroundServices
{
    public class BotRunnerService(
    ITelegramBotClient bot,
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
