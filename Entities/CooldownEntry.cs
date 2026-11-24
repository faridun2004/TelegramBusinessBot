namespace TelegramBusinessBot.Entities
{
    public sealed record CooldownEntry(
        string Category,
        long ChatId,
        DateTimeOffset LastReplyAt);
}
