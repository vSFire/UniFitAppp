using Microsoft.AspNetCore.Identity;

namespace UniFitApp.Models
{
    // Наследуемся от IdentityUser - это дает нам поля Email, PasswordHash, PhoneNumber и т.д. автоматом
    public class AppUser : IdentityUser
    {
        // Добавляем свои поля, которых нет в стандарте, но могут пригодиться по ТЗ
        public string FirstName { get; set; } // Имя
        public string LastName { get; set; }  // Фамилия
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}