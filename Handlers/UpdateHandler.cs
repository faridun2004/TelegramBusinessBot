using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBusinessBot.Configuration;
using TelegramBusinessBot.Services.Interfaces;

namespace TelegramBusinessBot.Handlers
{
    public class UpdateHandler(
    ITelegramBotClient bot,
    IBusinessConnectionStore store,
    IReplyEngine engine,
    IOptions<BotConfig> options,
    ILogger<UpdateHandler> log)
    {
        private readonly BotConfig _config = options.Value;

        private readonly ConcurrentDictionary<int, byte> _processedUpdates = new();

        private readonly ConcurrentQueue<int> _processedQueue = new();

        public async Task HandleAsync(
            Update update,
            CancellationToken ct)
        {
            if (!_processedUpdates.TryAdd(update.Id, 0))
                return;

            try
            {
                switch (update)
                {
                    case { BusinessConnection: { } connection }:
                        store.Set(connection);

                        log.LogInformation(
                            "Business connection {Id}: enabled={Enabled}",
                            connection.Id,
                            connection.IsEnabled);

                        break;

                    case { BusinessMessage: { } message }:
                        await OnBusinessMessage(message, ct);
                        break;

                    case { Message: { Text: { } } message }
                        when message.Chat.Type == ChatType.Private:

                        if (!string.IsNullOrWhiteSpace(
                                _config.Messages.DirectMessage))
                        {
                            await bot.SendMessage(
                                message.Chat.Id,
                                _config.Messages.DirectMessage,
                                cancellationToken: ct);
                        }

                        break;
                }

                _processedQueue.Enqueue(update.Id);

                // Ограничиваем размер хранилища.
                while (_processedQueue.Count > 5000)
                {
                    if (_processedQueue.TryDequeue(out var oldId))
                        _processedUpdates.TryRemove(oldId, out _);
                }
            }
            catch
            {
                // При ошибке разрешаем повторную обработку.
                _processedUpdates.TryRemove(update.Id, out _);
                throw;
            }
        }

        private async Task OnBusinessMessage(
            Message message,
            CancellationToken ct)
        {
            var connectionId = message.BusinessConnectionId;

            if (string.IsNullOrWhiteSpace(connectionId))
                return;

            if (string.IsNullOrWhiteSpace(message.Text))
                return;

            if (message.From is null)
                return;

            if (_config.Settings.IgnoreBotMessages && message.From.IsBot)
                return;

            if (!store.TryGet(connectionId, out var connection) ||
                connection is null)
            {
                connection = await bot.GetBusinessConnection(
                    connectionId,
                    ct);

                store.Set(connection);
            }

            if (!connection.IsEnabled)
                return;

            // Не отвечаем владельцу аккаунта.
            if (message.From.Id == connection.User.Id)
                return;

            var result = engine.GetReply(
                message.Chat.Id,
                message.Text);

            if (result is null)
                return;

            log.LogInformation(
                "Reply category: {Category}, chat: {ChatId}",
                result.Category,
                message.Chat.Id);

            var delay = _config.Settings.TypingDelayMilliseconds;

            if (delay > 0)
            {
                await bot.SendChatAction(
                    message.Chat.Id,
                    ChatAction.Typing,
                    businessConnectionId: connectionId,
                    cancellationToken: ct);

                await Task.Delay(delay, ct);
            }

            await bot.SendMessage(
                chatId: message.Chat.Id,
                text: result.Text,
                businessConnectionId: connectionId,
                cancellationToken: ct);
        }
    }
}
