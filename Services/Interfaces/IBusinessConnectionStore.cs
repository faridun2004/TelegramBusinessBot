using Telegram.Bot.Types;

namespace TelegramBusinessBot.Services.Interfaces
{
    public interface IBusinessConnectionStore
    {
        void Set(BusinessConnection connection);

        bool TryGet(
            string id,
            out BusinessConnection? connection);
    }
}
