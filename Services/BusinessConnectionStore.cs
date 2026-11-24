using System.Collections.Concurrent;
using Telegram.Bot.Types;
using TelegramBusinessBot.Services.Interfaces;

namespace TelegramBusinessBot.Services
{
    public class BusinessConnectionStore : IBusinessConnectionStore
    {
        private readonly ConcurrentDictionary<string, BusinessConnection> _connections = new();

        public void Set(BusinessConnection connection)
        {
            if (connection.IsEnabled)
            {
                _connections[connection.Id] = connection;
            }
            else
            {
                _connections.TryRemove(connection.Id, out _);
            }
        }

        public bool TryGet(
            string id,
            out BusinessConnection? connection)
        {
            return _connections.TryGetValue(id, out connection);
        }
    }
}
