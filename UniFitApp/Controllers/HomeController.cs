using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniFitApp.Data;
using UniFitApp.Models;

namespace UniFitApp.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public HomeController(ApplicationDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // 1. РАСПИСАНИЕ
        public async Task<IActionResult> Index(DateTime? date, bool showBookedOnly = false)
        {
            var user = await _userManager.GetUserAsync(User);
            var selectedDate = date.HasValue ? DateTime.SpecifyKind(date.Value, DateTimeKind.Utc) : DateTime.UtcNow.Date;

            ViewBag.SelectedDate = selectedDate;
            ViewBag.ShowBookedOnly = showBookedOnly;

            List<Workout> workouts;
            if (showBookedOnly)
            {
                workouts = await _context.Workouts
                    .Include(w => w.Coach).Include(w => w.Enrollments)
                    .Where(w => w.Enrollments.Any(e => e.StudentId == user.Id))
                    .OrderBy(w => w.StartTime).ToListAsync();
            }
            else
            {
                workouts = await _context.Workouts
                    .Include(w => w.Coach).Include(w => w.Enrollments)
                    .Where(w => w.StartTime >= selectedDate && w.StartTime < selectedDate.AddDays(1))
                    .OrderBy(w => w.StartTime).ToListAsync();
            }
            return View(workouts);
        }

        // 2. ЗАПИСЬ / ОТМЕНА ЗАПИСИ
        [Authorize(Roles = "Student")]
        [HttpPost]
        public async Task<IActionResult> Book(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var workout = await _context.Workouts.Include(w => w.Enrollments).FirstOrDefaultAsync(w => w.Id == id);

            if (workout == null) return NotFound();

            var existing = await _context.Enrollments.FirstOrDefaultAsync(e => e.WorkoutId == id && e.StudentId == user.Id);

            if (existing != null)
            {
                // === ОТМЕНА (Уведомление в историю на русском) ===
                _context.Notifications.Add(new Notification
                {
                    UserId = user.Id,
                    Message = $"Вы отменили запись на тренировку '{workout.Title}' ({workout.StartTime.ToLocalTime():dd.MM}).",
                    CreatedAt = DateTime.UtcNow
                });

                _context.Enrollments.Remove(existing);
                TempData["Info"] = "Вы отменили запись.";
            }
            else
            {
                // === ЗАПИСЬ (Уведомление в историю на русском) ===
                if (workout.Enrollments.Count >= workout.Capacity)
                {
                    TempData["Error"] = "Извините, мест больше нет!";
                    return RedirectToAction(nameof(Index));
                }

                _context.Enrollments.Add(new Enrollment { WorkoutId = id, StudentId = user.Id });

                _context.Notifications.Add(new Notification
                {
                    UserId = user.Id,
                    Message = $"Успешно! Вы записаны на '{workout.Title}' в {workout.StartTime.ToLocalTime():HH:mm}.",
                    CreatedAt = DateTime.UtcNow
                });

                TempData["Success"] = "Вы успешно записаны!";
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { date = workout.StartTime.ToString("yyyy-MM-dd") });
        }

        [Authorize(Roles = "Student")]
        public async Task<IActionResult> MyWorkouts()
        {
            var user = await _userManager.GetUserAsync(User);
            var myEnrollments = await _context.Enrollments
                .Include(e => e.Workout).ThenInclude(w => w.Coach)
                .Where(e => e.StudentId == user.Id)
                .OrderBy(e => e.Workout.StartTime).ToListAsync();
            return View(myEnrollments);
        }

        [Authorize(Roles = "Student")]
        [HttpPost]
        public async Task<IActionResult> CancelBooking(int workoutId)
        {
            return await Book(workoutId);
        }

        [Authorize(Roles = "Student")]
        public async Task<IActionResult> Attendance()
        {
            var user = await _userManager.GetUserAsync(User);
            return View(user);
        }
    }
}