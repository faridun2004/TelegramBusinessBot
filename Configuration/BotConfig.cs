namespace TelegramBusinessBot.Configuration
{
    public class BotConfig
    {
        public string Token { get; set; } = string.Empty;

        public string PublicUrl { get; set; } = string.Empty;

        public string SecretToken { get; set; } = string.Empty;

        public string OwnerName { get; set; } = string.Empty;

        public string OwnerRole { get; set; } = string.Empty;

        public BotSettings Settings { get; set; } = new();

        public BotCooldowns Cooldowns { get; set; } = new();

        public BotMessages Messages { get; set; } = new();

        public List<string> Priorities { get; set; } = [];

        public Dictionary<string, KeywordCategoryOptions> Keywords { get; set; } = new();
    }
}
