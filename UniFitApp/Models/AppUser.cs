using Microsoft.AspNetCore.Identity;

namespace UniFitApp.Models
{
    public class AppUser : IdentityUser
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? ProfilePictureUrl { get; set; }

        // === НОВЫЕ ПОЛЯ ДЛЯ ПРОФИЛЯ ТРЕНЕРА ===
        public string? Bio { get; set; } // О себе
        public string? Specialization { get; set; } // Йога, Бокс и т.д.
    }
}