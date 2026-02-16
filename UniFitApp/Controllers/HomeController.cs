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
        // 1. РАСПИСАНИЕ С ПОИСКОМ И ФИЛЬТРАМИ
        public async Task<IActionResult> Index(
            DateTime? date,
            bool showBookedOnly = false,
            string searchString = null,
            string workoutType = null,
            string timeOfDay = null)
        {
            var user = await _userManager.GetUserAsync(User);
            var selectedDate = date.HasValue ? DateTime.SpecifyKind(date.Value, DateTimeKind.Utc) : DateTime.UtcNow.Date;

            // Сохраняем параметры для View, чтобы фильтры не сбрасывались
            ViewBag.SelectedDate = selectedDate;
            ViewBag.ShowBookedOnly = showBookedOnly;
            ViewBag.CurrentSearch = searchString;
            ViewBag.CurrentType = workoutType;
            ViewBag.CurrentTime = timeOfDay;

            // Начальный запрос
            var query = _context.Workouts
                .Include(w => w.Coach)
                .Include(w => w.Enrollments)
                .AsQueryable();

            // 1. Фильтр: Только забронированные (если включено)
            if (showBookedOnly)
            {
                query = query.Where(w => w.Enrollments.Any(e => e.StudentId == user.Id));
            }
            else
            {
                // Иначе фильтруем по дате (только на выбранный день)
                query = query.Where(w => w.StartTime >= selectedDate && w.StartTime < selectedDate.AddDays(1));
            }

            // 2. Поиск по названию (Search)
            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(w => w.Title.ToLower().Contains(searchString.ToLower()));
            }

            // 3. Фильтр по Типу (Cardio, Strength...)
            if (!string.IsNullOrEmpty(workoutType) && workoutType != "All")
            {
                query = query.Where(w => w.Type == workoutType);
            }

            // 4. Фильтр по Времени суток
            if (!string.IsNullOrEmpty(timeOfDay) && timeOfDay != "All")
            {
                // Для корректного сравнения времени в БД (Postgres) лучше вытянуть данные
                // Но для оптимизации попробуем через Hour
                if (timeOfDay == "Morning") // до 12:00
                {
                    query = query.Where(w => w.StartTime.Hour < 12);
                }
                else if (timeOfDay == "Afternoon") // 12:00 - 17:00
                {
                    query = query.Where(w => w.StartTime.Hour >= 12 && w.StartTime.Hour < 17);
                }
                else if (timeOfDay == "Evening") // после 17:00
                {
                    query = query.Where(w => w.StartTime.Hour >= 17);
                }
            }

            var workouts = await query.OrderBy(w => w.StartTime).ToListAsync();
            return View(workouts);
        }

        // === МЕТОД: ДЕТАЛИ ТРЕНИРОВКИ (ВИДЕО) ===
        public async Task<IActionResult> Details(int id)
        {
            var workout = await _context.Workouts
                .Include(w => w.Coach)
                .Include(w => w.Enrollments)
                .FirstOrDefaultAsync(w => w.Id == id);

            if (workout == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            ViewBag.CurrentUserId = user.Id;

            return View(workout);
        }

        // === МЕТОД: ПУБЛИЧНЫЙ ПРОФИЛЬ ТРЕНЕРА ===
        public async Task<IActionResult> CoachProfile(string coachId)
        {
            var coach = await _userManager.FindByIdAsync(coachId);
            if (coach == null) return NotFound();

            // Загружаем будущие тренировки ЭТОГО тренера
            var upcomingWorkouts = await _context.Workouts
                .Where(w => w.CoachId == coachId && w.StartTime > DateTime.UtcNow)
                .OrderBy(w => w.StartTime)
                .ToListAsync();

            ViewBag.Workouts = upcomingWorkouts;

            return View(coach);
        }

        // 2. ЗАПИСЬ / ОТМЕНА ЗАПИСИ
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
                // ОТМЕНА
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
                // ЗАПИСЬ
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

            // Если пришли со страницы деталей - вернем туда же
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
}