using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniFitApp.Data;
using UniFitApp.Models;

namespace UniFitApp.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly IWebHostEnvironment _appEnvironment; // <--- ДОБАВИЛИ ЭТО
        private readonly ApplicationDbContext _context;
        public AccountController(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager, IWebHostEnvironment appEnvironment)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _appEnvironment = appEnvironment; // <--- И ЭТО
        }

        // GET: Страница регистрации
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        // POST: Обработка данных формы
        [HttpPost]
        public async Task<IActionResult> Register(string email, string password, string firstName, string lastName, string userRole)
        {
            if (ModelState.IsValid)
            {
                var user = new AppUser { UserName = email, Email = email, FirstName = firstName, LastName = lastName };

                // Создаем пользователя (пароль захешируется сам)
                var result = await _userManager.CreateAsync(user, password);

                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, userRole);
                    await _signInManager.SignInAsync(user, isPersistent: false);

                    // === ПРОВЕРКА РОЛИ ПОСЛЕ РЕГИСТРАЦИИ ===
                    if (userRole == "Coach")
                    {
                        return RedirectToAction("Index", "Coach");
                    }
                    // =======================================

                    return RedirectToAction("Index", "Home");
                }

                // Если ошибки (например, пароль простой), выводим их
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            return View();
        }
        // --- ЛОГИН (ВХОД) ---

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            if (ModelState.IsValid)
            {
                // Пытаемся войти (false = не запоминать меня навечно)
                var result = await _signInManager.PasswordSignInAsync(email, password, false, false);

                if (result.Succeeded)
                {
                    // Проверяем роль пользователя
                    var user = await _userManager.FindByEmailAsync(email);
                    if (await _userManager.IsInRoleAsync(user, "Coach"))
                    {
                        return RedirectToAction("Index", "Coach"); // Тренера -> в Дашборд
                    }

                    return RedirectToAction("Index", "Home"); // Студента -> в Расписание
                }

                ModelState.AddModelError(string.Empty, "Неверный логин или пароль");
            }
            return View();
        }

        // --- ЛОГАУТ (ВЫХОД) ---

        [HttpPost] // Важно: выход только через POST запрос для безопасности
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            // Загружаем пользователя из базы, чтобы получить ссылку на фото и свежие данные
            var user = await _userManager.GetUserAsync(User);
            return View(user);
        }
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
        // СТРАНИЦА: Настройки профиля
        [HttpGet]
        public async Task<IActionResult> Settings()
        {
            var user = await _userManager.GetUserAsync(User);
            return View(user);
        }

        // ДЕЙСТВИЕ: Сохранить настройки и фото
        [HttpPost]
        public async Task<IActionResult> Settings(AppUser model, IFormFile? avatarFile)
        {
            var user = await _userManager.GetUserAsync(User);

            // Обновляем имена
            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.PhoneNumber = model.PhoneNumber;

            // Если загрузили новое фото
            if (avatarFile != null)
            {
                // 1. Придумываем уникальное имя файлу (чтобы не затереть другие)
                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(avatarFile.FileName);

                // 2. Путь к папке wwwroot/avatars
                string path = Path.Combine(_appEnvironment.WebRootPath, "avatars");
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);

                // 3. Сохраняем файл на диск
                using (var fileStream = new FileStream(Path.Combine(path, fileName), FileMode.Create))
                {
                    await avatarFile.CopyToAsync(fileStream);
                }

                // 4. Записываем путь в базу
                user.ProfilePictureUrl = "/avatars/" + fileName;
            }

            await _userManager.UpdateAsync(user);
            return RedirectToAction("Profile");
        }
        [HttpGet]
        public async Task<IActionResult> Notifications()
        {
            var user = await _userManager.GetUserAsync(User);

            // Загружаем уведомления (сначала новые)
            var notifications = _context.Notifications
                .Where(n => n.UserId == user.Id)
                .OrderByDescending(n => n.CreatedAt)
                .ToList();

            return View(notifications);
        }
        [HttpGet]
        public IActionResult Help() => View();
    }
}