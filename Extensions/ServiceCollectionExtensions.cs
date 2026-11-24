using Telegram.Bot;
using TelegramBusinessBot.BackgroundServices;
using TelegramBusinessBot.Configuration;
using TelegramBusinessBot.Services;
using TelegramBusinessBot.Services.Interfaces;

namespace TelegramBusinessBot.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddTelegramBusinessBot(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<BotConfig>(
                configuration.GetSection("Bot"));

            services.AddSingleton<ITelegramBotClient>(provider =>
            {
                var options = provider
                    .GetRequiredService<
                        Microsoft.Extensions.Options.IOptions<BotConfig>>();

                return new TelegramBotClient(options.Value.Token);
            });

            services.AddSingleton<
                IBusinessConnectionStore,
                BusinessConnectionStore>();

            services.AddSingleton<
                IKeywordMatcher,
                KeywordMatcher>();

            services.AddSingleton<
                ICooldownService,
                CooldownService>();

            services.AddSingleton<
                IReplyEngine,
                ReplyEngine>();

            services.AddHostedService<BotRunnerService>();

            return services;
        }
    }
}
