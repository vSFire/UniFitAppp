using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using UniFitApp.Models;

namespace UniFitApp.Data
{
    // ВАЖНО: Наследуемся от IdentityDbContext, чтобы появились таблицы ролей и юзеров
    public class ApplicationDbContext : IdentityDbContext<AppUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        public DbSet<Workout> Workouts { get; set; }     // Тренировки
        public DbSet<Enrollment> Enrollments { get; set; } // Записи студентов
    }

}