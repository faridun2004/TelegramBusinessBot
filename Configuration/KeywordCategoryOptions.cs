namespace TelegramBusinessBot.Configuration
{
    public class KeywordCategoryOptions
    {
        public bool Enabled { get; set; } = true;

        public int CooldownMinutes { get; set; } = 60;

        public List<string> Triggers { get; set; } = [];

        public string Response { get; set; } = string.Empty;
    }
}