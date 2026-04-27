using System.ComponentModel.DataAnnotations;

namespace EShopModel.Entities
{
    public class ContactMessage
    {
        [Key]
        public int Id { get; set; }

        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Message { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string Status { get; set; } = "Active";
    }
}
