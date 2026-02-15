using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using UniFitApp.Models;

namespace UniFitApp.Controllers
{
    [Authorize] // Страница доступна только после входа
    public class WelcomeController : Controller
    {
        private readonly UserManager<AppUser> _userManager;

        public WelcomeController(UserManager<AppUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            return View(user);
        }
    }
}