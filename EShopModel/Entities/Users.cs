using Microsoft.AspNetCore.Identity;
namespace EShopModel.Entities
{
    public class Users : IdentityUser
    {
        public string? FullName { get; set; }
        public string? Role { get; set; }
    }
}
