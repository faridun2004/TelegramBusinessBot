using Microsoft.Extensions.Options;
using TelegramBusinessBot.Configuration;
using TelegramBusinessBot.Entities;
using TelegramBusinessBot.Services.Interfaces;

namespace TelegramBusinessBot.Services
{
    public class ReplyEngine(
    IOptions<BotConfig> options,
    IKeywordMatcher matcher,
    ICooldownService cooldownService) : IReplyEngine
    {
        private readonly BotConfig _config = options.Value;

        public ReplyResult? GetReply(long chatId, string text)
        {
            if (!_config.Settings.Enabled ||
                string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            // 1. Распознавание сообщения.
            if (_config.Settings.EnableKeywordReplies)
            {
                var match = matcher.Match(text);

                if (match is not null)
                {
                    var category = _config.Keywords[match.Category];

                    var cooldown = TimeSpan.FromMinutes(
                        category.CooldownMinutes);

                    // Категория распознана, но отвечать пока нельзя.
                    if (!cooldownService.TryAcquire(
                            chatId,
                            match.Category,
                            cooldown))
                    {
                        return null;
                    }

                    return new ReplyResult(
                        match.Category,
                        Format(category.Response));
                }
            }

            // 2. Если ничего не найдено, используем Fallback.
            if (string.IsNullOrWhiteSpace(_config.Messages.Fallback))
                return null;

            var fallbackCooldown = TimeSpan.FromMinutes(
                _config.Cooldowns.FallbackMinutes);

            if (!cooldownService.TryAcquire(
                    chatId,
                    "Fallback",
                    fallbackCooldown))
            {
                return null;
            }

            return new ReplyResult(
                "Fallback",
                Format(_config.Messages.Fallback));
        }

        private string Format(string template)
        {
            return template
                .Replace("{owner}", _config.OwnerName)
                .Replace("{role}", _config.OwnerRole);
        }
    }
}
