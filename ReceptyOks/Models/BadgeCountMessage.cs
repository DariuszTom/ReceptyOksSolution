using System;
using System.Collections.Generic;
using System.Text;

namespace ReceptyOks.Models
{
    public class BadgeCountMessage
    {
        public uint Count { get; }
        public BadgeCountMessage(uint count) => Count = count;
    }
}
