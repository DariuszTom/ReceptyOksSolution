using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace ReceptyOks.Shared.Models
{
    [Serializable()]
    public class User
    {
        public User()
        {
            AddOn = DateTime.UtcNow;
        }
        [DataMember]
        public Guid Id { get; set; }
        [DataMember]
        public string Name { get; set; }
        [DataMember]
        public string Email { get; set; }= string.Empty;
        [DataMember]
        public string PhoneNumber { get; set; } = string.Empty;
        [DataMember]
        public DateTime? AddOn { get; init; }
    }
}
