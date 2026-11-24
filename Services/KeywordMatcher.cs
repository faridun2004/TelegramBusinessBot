using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using TelegramBusinessBot.Configuration;
using TelegramBusinessBot.Entities;
using TelegramBusinessBot.Services.Interfaces;

namespace TelegramBusinessBot.Services
{
    public class KeywordMatcher(
        IOptions<BotConfig> options) : IKeywordMatcher
    {
        private readonly BotConfig _config = options.Value;

        private readonly Dictionary<string, int> _priorities =
            options.Value.Priorities
                .Select((name, index) => new { name, index })
                .DistinctBy(x => x.name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    x => x.name,
                    x => x.index,
                    StringComparer.OrdinalIgnoreCase);

        public KeywordMatch? Match(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var normalizedText = Normalize(text);

            var matches = new List<KeywordMatch>();

            foreach (var (categoryName, category) in _config.Keywords)
            {
                if (!category.Enabled)
                    continue;

                foreach (var trigger in category.Triggers)
                {
                    if (string.IsNullOrWhiteSpace(trigger))
                        continue;

                    var normalizedTrigger = Normalize(trigger);

                    if (!IsMatch(normalizedText, normalizedTrigger))
                        continue;

                    var priority = _priorities.TryGetValue(
                        categoryName,
                        out var value)
                        ? value
                        : int.MaxValue;

                    matches.Add(new KeywordMatch(
                        categoryName,
                        normalizedTrigger,
                        priority));

                    break;
                }
            }

            return matches
                .OrderBy(x => x.Priority)
                .ThenByDescending(x => x.Trigger.Length)
                .FirstOrDefault();
        }

        private bool IsMatch(string text, string trigger)
        {
            if (_config.Settings.MatchWholeWords)
            {
                var pattern =
                    $@"(?<![\p{{L}}\p{{N}}_]){Regex.Escape(trigger)}(?![\p{{L}}\p{{N}}_])";

                var regexOptions = _config.Settings.CaseInsensitive
                    ? RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
                    : RegexOptions.None;

                return Regex.IsMatch(text, pattern, regexOptions);
            }

            return text.Contains(
                trigger,
                _config.Settings.CaseInsensitive
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal);
        }

        private string Normalize(string text)
        {
            if (_config.Settings.NormalizeText)
            {
                text = text
                    .Replace('ё', 'е')
                    .Replace('Ё', 'Е');

                text = Regex.Replace(text, @"\s+", " ");
            }

            if (_config.Settings.CaseInsensitive)
                text = text.ToLowerInvariant();

            return text.Trim();
        }
    }
}
