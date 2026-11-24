using System.Collections.Concurrent;
using TelegramBusinessBot.Entities;
using TelegramBusinessBot.Services.Interfaces;

namespace TelegramBusinessBot.Services
{
    public class CooldownService : ICooldownService
    {
        private readonly ConcurrentDictionary<string, CooldownEntry> _entries = new();

        public bool TryAcquire(
            long chatId,
            string category,
            TimeSpan cooldown)
        {
            var key = $"{chatId}:{category}";

            var now = DateTimeOffset.UtcNow;

            while (true)
            {
                if (!_entries.TryGetValue(key, out var existing))
                {
                    var entry = new CooldownEntry(
                        category,
                        chatId,
                        now);

                    if (_entries.TryAdd(key, entry))
                        return true;

                    continue;
                }

                if (now - existing.LastReplyAt < cooldown)
                    return false;

                var updated = new CooldownEntry(
                    category,
                    chatId,
                    now);

                if (_entries.TryUpdate(key, updated, existing))
                    return true;
            }
        }
    }
}
