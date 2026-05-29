using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniFitApp.Data;
using UniFitApp.Models;
// Не забудь убедиться, что у тебя есть using UniFitApp.Services; если он в другой папке!

namespace UniFitApp.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly IWebHostEnvironment _appEnvironment;
        private readonly ApplicationDbContext _context;
        // ДОБАВЛЕНО: сервис для писем
        private readonly UniFitApp.Services.EmailService _emailService;

        // ДОБАВЛЕНО: emailService в параметры конструктора
        public AccountController(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager, IWebHostEnvironment appEnvironment, ApplicationDbContext context, UniFitApp.Services.EmailService emailService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _appEnvironment = appEnvironment;
            _context = context;
            _emailService = emailService; // Инициализация
        }

        // GET: Страница регистрации
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        // POST: Обработка данных формы
        [HttpPost]
        public async Task<IActionResult> Register(string email, string password, string confirmPassword, string firstName, string lastName, string userRole)
        {
            if (password != confirmPassword)
            {
                ModelState.AddModelError(string.Empty, "Пароли не совпадают!");
                return View();
            }

            if (ModelState.IsValid)
            {
                var user = new AppUser { UserName = email, Email = email, FirstName = firstName, LastName = lastName };

                // Создаем пользователя
                var result = await _userManager.CreateAsync(user, password);

                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, userRole);
                    await _signInManager.SignInAsync(user, isPersistent: false);

                    // ПЕРЕНАПРАВЛЕНИЕ НА WELCOME
                    return RedirectToAction("Index", "Welcome");
                }

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
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Welcome");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            if (ModelState.IsValid)
            {
                var result = await _signInManager.PasswordSignInAsync(email, password, false, false);

                if (result.Succeeded)
                {
                    return RedirectToAction("Index", "Welcome");
                }

                ModelState.AddModelError(string.Empty, "Неверный логин или пароль");
            }
            return View();
        }

        // --- ЛОГАУТ (ВЫХОД) ---
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
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

        // ДЕЙСТВИЕ: Сохранить настройки (ОБНОВЛЕНО: БЕЗОПАСНОЕ СОХРАНЕНИЕ ФОТО)
        // ДЕЙСТВИЕ: Сохранить настройки (РЕЖИМ ЖЕСТКОЙ ОТЛАДКИ)
        [HttpPost]
        public async Task<IActionResult> Settings(AppUser model, IFormFile? avatarFile)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return Content("ОШИБКА: Пользователь не найден.");

                user.FirstName = model.FirstName;
                user.LastName = model.LastName;
                user.PhoneNumber = model.PhoneNumber;
                user.Bio = model.Bio;
                user.Specialization = model.Specialization;

                if (avatarFile != null)
                {
                    string webRootPath = _appEnvironment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                    string fileName = Guid.NewGuid().ToString() + Path.GetExtension(avatarFile.FileName);
                    string path = Path.Combine(webRootPath, "avatars");

                    if (!Directory.Exists(path))
                    {
                        Directory.CreateDirectory(path);
                    }

                    using (var fileStream = new FileStream(Path.Combine(path, fileName), FileMode.Create))
                    {
                        await avatarFile.CopyToAsync(fileStream);
                    }

                    user.ProfilePictureUrl = "/avatars/" + fileName;
                }

                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    return Content("ОШИБКА СОХРАНЕНИЯ В БД: " + string.Join(", ", updateResult.Errors.Select(e => e.Description)));
                }

                return RedirectToAction("Profile");
            }
            catch (Exception ex)
            {
                // Если ошибка в нашем коде или правах доступа, мы увидим её тут:
                return Content($"КРИТИЧЕСКАЯ ОШИБКА В КОДЕ:\n\nСообщение: {ex.Message}\n\nГде упало: {ex.StackTrace}");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Notifications()
        {
            var user = await _userManager.GetUserAsync(User);

            var notifications = _context.Notifications
                .Where(n => n.UserId == user.Id)
                .OrderByDescending(n => n.CreatedAt)
                .ToList();

            return View(notifications);
        }

        [HttpGet]
        public IActionResult Help() => View();

        // ==========================================
        // ДОБАВЛЕНО: МЕТОДЫ ДЛЯ СБРОСА ПАРОЛЯ
        // ==========================================

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            if (string.IsNullOrEmpty(email)) return View();

            var user = await _userManager.FindByEmailAsync(email);
            if (user != null)
            {
                // Генерируем секретный токен для пользователя
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);

                // Создаем ссылку
                var resetLink = Url.Action("ResetPassword", "Account", new { token, email = user.Email }, Request.Scheme);

                // Отправляем письмо
                var message = $"<h4>Восстановление доступа к UniFitApp</h4>" +
                              $"<p>Вы запросили сброс пароля. Чтобы установить новый пароль, перейдите по <a href='{resetLink}'>этой ссылке</a>.</p>";

                await _emailService.SendEmailAsync(user.Email, "Сброс пароля", message);
            }

            TempData["SuccessMessage"] = "Если этот email зарегистрирован в системе, мы отправили на него ссылку для сброса пароля.";
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult ResetPassword(string token, string email)
        {
            if (token == null || email == null) return RedirectToAction("Index", "Home");

            ViewBag.Token = token;
            ViewBag.Email = email;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(string email, string token, string newPassword)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null) return RedirectToAction("Login");

            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Пароль успешно изменен! Теперь вы можете войти.";
                return RedirectToAction("Login");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            ViewBag.Token = token;
            ViewBag.Email = email;
            return View();
        }
    }
}