using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniFitApp.Data;
using UniFitApp.Models;

namespace UniFitApp.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public AdminController(ApplicationDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public IActionResult Index()
        {
            return RedirectToAction("Users");
        }

        public async Task<IActionResult> Users()
        {
            var users = await _userManager.Users
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();
            return View(users);
        }

        // === ДОБАВЛЕНО: Метод удаления пользователя ===
        [HttpPost]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            // Защита от удаления главного админа
            if (user.Email == "admin@unifit.com")
            {
                TempData["Error"] = "Вы не можете удалить Супер-Админа!";
                return RedirectToAction("Users");
            }

            var result = await _userManager.DeleteAsync(user);

            if (result.Succeeded)
            {
                TempData["Success"] = $"Пользователь {user.Email} успешно удален.";
            }
            else
            {
                TempData["Error"] = "Произошла ошибка при удалении пользователя.";
            }

            return RedirectToAction("Users");
        }
        // ===============================================

        public async Task<IActionResult> Workouts()
        {
            var workouts = await _context.Workouts
                .Include(w => w.Coach)
                .Include(w => w.Enrollments)
                .OrderByDescending(w => w.StartTime)
                .ToListAsync();

            return View(workouts);
        }
    }
}