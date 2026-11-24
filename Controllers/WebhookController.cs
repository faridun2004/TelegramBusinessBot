using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types;
using TelegramBusinessBot.Configuration;
using TelegramBusinessBot.Handlers;

namespace TelegramBusinessBot.Controllers
{
    [ApiController]
    [Route("webhook")]
    public class WebhookController(
    UpdateHandler handler,
    IOptions<BotConfig> options,
    ILogger<WebhookController> log) : ControllerBase
    {
        private readonly BotConfig _config = options.Value;

        [HttpPost]
        public async Task<IActionResult> Receive(
            CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(_config.SecretToken))
            {
                var secret = Request.Headers[
                    "X-Telegram-Bot-Api-Secret-Token"].ToString();

                if (!string.Equals(
                        secret,
                        _config.SecretToken,
                        StringComparison.Ordinal))
                {
                    log.LogWarning("Invalid webhook secret token");

                    return Unauthorized();
                }
            }

            var update = await Request.ReadFromJsonAsync<Update>(
                cancellationToken: ct);

            if (update is null)
                return BadRequest();

            try
            {
                await handler.HandleAsync(update, ct);

                return Ok();
            }
            catch (Exception ex)
            {
                log.LogError(
                    ex,
                    "Webhook update processing failed: {UpdateId}",
                    update.Id);

                return StatusCode(500);
            }
        }
    }
}
