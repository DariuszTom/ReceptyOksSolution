using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace ReceptyOks.Shared.Models
{
    [Serializable()]
    public class User
    {
        public User()
        {
            AddOn = DateTime.UtcNow;
            Id = Guid.NewGuid();
        }

        [DataMember]
        public Guid Id { get; init; }

        [DataMember]
        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [DataMember]
        [EmailAddress]
        [MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        [DataMember]
        [Phone]
        [MaxLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        [DataMember]
        public DateTime? AddOn { get; init; }
    }
}
