namespace TelegramBusinessBot.Entities
{
    public sealed record KeywordMatch(
      string Category,
      string Trigger,
      int Priority);
}
