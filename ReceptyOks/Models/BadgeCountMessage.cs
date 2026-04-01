namespace ReceptyOks.Models
{
    public class BadgeCountMessage
    {
        public uint Count { get; }
        public BadgeCountMessage(uint count) => Count = count;
    }
}
