using TelegramBusinessBot.Entities;

namespace TelegramBusinessBot.Services.Interfaces
{
    public interface IReplyEngine
    {
        ReplyResult? GetReply(long chatId, string text);
    }
}
