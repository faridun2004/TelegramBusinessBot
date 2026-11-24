namespace TelegramBusinessBot.Services.Interfaces
{
    public interface ICooldownService
    {
        bool TryAcquire(
            long chatId,
            string category,
            TimeSpan cooldown);
    }
}
