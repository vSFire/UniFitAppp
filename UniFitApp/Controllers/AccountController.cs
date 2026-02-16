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
        private readonly IWebHostEnvironment _appEnvironment;
        private readonly ApplicationDbContext _context;

        public AccountController(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager, IWebHostEnvironment appEnvironment, ApplicationDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _appEnvironment = appEnvironment;
            _context = context;
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

        // ДЕЙСТВИЕ: Сохранить настройки (ОБНОВЛЕНО: BIO + SPEC)
        [HttpPost]
        public async Task<IActionResult> Settings(AppUser model, IFormFile? avatarFile)
        {
            var user = await _userManager.GetUserAsync(User);

            // Базовые поля
            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.PhoneNumber = model.PhoneNumber;

            // Новые поля для тренера
            user.Bio = model.Bio;
            user.Specialization = model.Specialization;

            // Если загрузили новое фото
            if (avatarFile != null)
            {
                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(avatarFile.FileName);
                string path = Path.Combine(_appEnvironment.WebRootPath, "avatars");
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);

                using (var fileStream = new FileStream(Path.Combine(path, fileName), FileMode.Create))
                {
                    await avatarFile.CopyToAsync(fileStream);
                }

                user.ProfilePictureUrl = "/avatars/" + fileName;
            }

            await _userManager.UpdateAsync(user);
            return RedirectToAction("Profile");
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
    }
}