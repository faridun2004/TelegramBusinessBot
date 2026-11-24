using TelegramBusinessBot.Entities;

namespace TelegramBusinessBot.Services.Interfaces
{
    public interface IKeywordMatcher
    {
        KeywordMatch? Match(string text);
    }
}
