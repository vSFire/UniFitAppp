using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniFitApp.Data;
using UniFitApp.Models;

namespace UniFitApp.Controllers
{
    // Разрешаем доступ всем (даже без входа - чтобы видели расписание), 
    // но записываться смогут только вошедшие.
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public HomeController(ApplicationDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            if (User.IsInRole("Coach"))
            {
                return RedirectToAction("Index", "Coach");
            }
            // Получаем список всех будущих тренировок, сортируем по времени
            var workouts = await _context.Workouts
                .Include(w => w.Coach)       // Подгружаем имя тренера
                .Include(w => w.Enrollments) // Подгружаем записи (чтобы считать места)
                .Where(w => w.StartTime >= DateTime.UtcNow.AddHours(-5)) // Только актуальные (с небольшим запасом)
                .OrderBy(w => w.StartTime)
                .ToListAsync();

            return View(workouts);
        }

        // Логика ЗАПИСИ (Book)
        [Authorize(Roles = "Student")] // Только студенты могут жать кнопку
        [HttpPost]
        public async Task<IActionResult> Book(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var workout = await _context.Workouts.Include(w => w.Enrollments).FirstOrDefaultAsync(w => w.Id == id);

            if (workout == null) return NotFound();

            // Проверка: Места есть?
            if (workout.Enrollments.Count >= workout.Capacity)
            {
                TempData["Error"] = "Извините, мест больше нет!";
                return RedirectToAction(nameof(Index));
            }

            // Проверка: Уже записан?
            if (workout.Enrollments.Any(e => e.StudentId == user.Id))
            {
                TempData["Info"] = "Вы уже записаны на это занятие.";
                return RedirectToAction(nameof(Index));
            }

            // Записываем
            var enrollment = new Enrollment { WorkoutId = id, StudentId = user.Id };
            _context.Enrollments.Add(enrollment);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Вы успешно записаны!";
            return RedirectToAction(nameof(Index));
        }
        // СТРАНИЦА: Мои тренировки (Upcoming & Past)
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> MyWorkouts()
        {
            var user = await _userManager.GetUserAsync(User);

            // Ищем записи текущего студента
            var myEnrollments = await _context.Enrollments
                .Include(e => e.Workout)            // Подгружаем инфо о тренировке
                .ThenInclude(w => w.Coach)          // И кто тренер
                .Where(e => e.StudentId == user.Id)
                .OrderBy(e => e.Workout.StartTime)  // Сортируем по времени
                .ToListAsync();

            return View(myEnrollments);
        }

        // ДЕЙСТВИЕ: Отменить запись (Cancel Booking)
        [Authorize(Roles = "Student")]
        [HttpPost]
        public async Task<IActionResult> CancelBooking(int workoutId)
        {
            var user = await _userManager.GetUserAsync(User);

            // Ищем запись
            var enrollment = await _context.Enrollments
                .FirstOrDefaultAsync(e => e.WorkoutId == workoutId && e.StudentId == user.Id);

            if (enrollment != null)
            {
                _context.Enrollments.Remove(enrollment);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Вы отменили запись на тренировку.";
            }
            else
            {
                TempData["Error"] = "Запись не найдена.";
            }

            return RedirectToAction(nameof(MyWorkouts));
        }
        // СТРАНИЦА: Цифровой пропуск (QR код)
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> Attendance()
        {
            var user = await _userManager.GetUserAsync(User);
            return View(user);
        }
    }
}