namespace TelegramBusinessBot.Configuration
{
    public class BotSettings
    {
        public bool Enabled { get; set; } = true;

        public bool IgnoreBotMessages { get; set; } = true;

        public bool EnableKeywordReplies { get; set; } = true;

        public bool EnableBilingualReplies { get; set; } = true;

        public bool CaseInsensitive { get; set; } = true;

        public bool MatchWholeWords { get; set; } = true;

        public bool NormalizeText { get; set; } = true;

        public int TypingDelayMilliseconds { get; set; } = 1000;
    }
}
