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

        // 1. РАСПИСАНИЕ (С ГРУППИРОВКОЙ ДЛЯ НОВОГО ДИЗАЙНА)
        public async Task<IActionResult> Index(
            DateTime? date,
            bool showBookedOnly = false,
            string searchString = null,
            string workoutType = null,
            string timeOfDay = null)
        {
            var user = await _userManager.GetUserAsync(User);
            var selectedDate = date.HasValue ? DateTime.SpecifyKind(date.Value, DateTimeKind.Utc) : DateTime.UtcNow.Date;

            ViewBag.SelectedDate = selectedDate;
            ViewBag.ShowBookedOnly = showBookedOnly;
            ViewBag.CurrentSearch = searchString;
            ViewBag.CurrentType = workoutType;
            ViewBag.CurrentTime = timeOfDay;

            var query = _context.Workouts
                .Include(w => w.Coach)
                .Include(w => w.Enrollments)
                .AsQueryable();

            if (showBookedOnly)
            {
                query = query.Where(w => w.Enrollments.Any(e => e.StudentId == user.Id));
            }
            else
            {
                // Показываем тренировки на выбранную дату
                query = query.Where(w => w.StartTime >= selectedDate && w.StartTime < selectedDate.AddDays(1));
            }

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(w => w.Title.ToLower().Contains(searchString.ToLower()));
            }

            if (!string.IsNullOrEmpty(workoutType) && workoutType != "All")
            {
                query = query.Where(w => w.Type == workoutType);
            }

            if (!string.IsNullOrEmpty(timeOfDay) && timeOfDay != "All")
            {
                if (timeOfDay == "Morning") query = query.Where(w => w.StartTime.Hour < 12);
                else if (timeOfDay == "Afternoon") query = query.Where(w => w.StartTime.Hour >= 12 && w.StartTime.Hour < 17);
                else if (timeOfDay == "Evening") query = query.Where(w => w.StartTime.Hour >= 17);
            }

            var rawWorkouts = await query.OrderBy(w => w.StartTime).ToListAsync();

            // === ГРУППИРОВКА (Магия для нового дизайна) ===
            // Мы группируем тренировки по названию. Если в один день есть три тренировки "CrossFit",
            // они соберутся в одну группу.
            var groupedWorkouts = rawWorkouts
                .GroupBy(w => new { w.Title, w.Description, w.Type })
                .Select(g => new WorkoutGroupViewModel
                {
                    Title = g.Key.Title,
                    Description = g.Key.Description,
                    Type = g.Key.Type,
                    Sessions = g.ToList() // Список конкретных тренировок (со временем и тренерами)
                })
                .ToList();

            ViewBag.CurrentUserId = user.Id;
            return View(groupedWorkouts);
        }

        // === МЕТОД: ДЕТАЛИ ТРЕНИРОВКИ (С БУДУЩИМИ СЕАНСАМИ) ===
        public async Task<IActionResult> Details(int id)
        {
            var workout = await _context.Workouts
                .Include(w => w.Coach)
                .Include(w => w.Enrollments)
                .FirstOrDefaultAsync(w => w.Id == id);

            if (workout == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            ViewBag.CurrentUserId = user.Id;

            // Загружаем все БУДУЩИЕ сеансы тренировки с таким же названием (для нового дизайна)
            var futureSessions = await _context.Workouts
                .Include(w => w.Enrollments)
                .Where(w => w.Title == workout.Title && w.StartTime > DateTime.UtcNow)
                .OrderBy(w => w.StartTime)
                .ToListAsync();

            ViewBag.FutureSessions = futureSessions;

            return View(workout);
        }

        public async Task<IActionResult> CoachProfile(string coachId)
        {
            var coach = await _userManager.FindByIdAsync(coachId);
            if (coach == null) return NotFound();

            var upcomingWorkouts = await _context.Workouts
                .Where(w => w.CoachId == coachId && w.StartTime > DateTime.UtcNow)
                .OrderBy(w => w.StartTime)
                .ToListAsync();

            ViewBag.Workouts = upcomingWorkouts;
            return View(coach);
        }

        [Authorize(Roles = "Student")]
        [HttpPost]
        public async Task<IActionResult> Book(int id, string? returnUrl = null)
        {
            var user = await _userManager.GetUserAsync(User);
            var workout = await _context.Workouts.Include(w => w.Enrollments).FirstOrDefaultAsync(w => w.Id == id);

            if (workout == null) return NotFound();

            var existing = await _context.Enrollments.FirstOrDefaultAsync(e => e.WorkoutId == id && e.StudentId == user.Id);

            if (existing != null)
            {
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

            if (!string.IsNullOrEmpty(returnUrl))
            {
                return Redirect(returnUrl);
            }

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

    // === ВСПОМОГАТЕЛЬНЫЙ КЛАСС ДЛЯ ГРУППИРОВКИ ===
    // Добавь его прямо в конец файла HomeController.cs
    public class WorkoutGroupViewModel
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Type { get; set; }
        public List<Workout> Sessions { get; set; }
    }
}