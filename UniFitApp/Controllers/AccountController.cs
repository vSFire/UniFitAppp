using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using UniFitApp.Models;

namespace UniFitApp.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;

        public AccountController(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
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
        public IActionResult Profile()
        {
            return View();
        }
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}