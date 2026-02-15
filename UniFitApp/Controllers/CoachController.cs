using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniFitApp.Data;
using UniFitApp.Models;

namespace UniFitApp.Controllers
{
    // ВАЖНО: Сюда пускаем только Тренеров!
    [Authorize(Roles = "Coach")]
    public class CoachController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public CoachController(ApplicationDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // 1. ДАШБОРД ТРЕНЕРА
        public async Task<IActionResult> Index(DateTime? date, bool showAll = false)
        {
            var user = await _userManager.GetUserAsync(User);

            // ИСПРАВЛЕНИЕ ОШИБКИ POSTGRESQL (UTC)
            var selectedDate = date.HasValue
                ? DateTime.SpecifyKind(date.Value, DateTimeKind.Utc)
                : DateTime.UtcNow.Date;

            ViewBag.SelectedDate = selectedDate;
            ViewBag.ShowAll = showAll; // Передаем флаг в View

            // Базовый запрос
            var query = _context.Workouts
                .Include(w => w.Enrollments)
                .Include(w => w.Coach)
                .Where(w => w.StartTime >= selectedDate && w.StartTime < selectedDate.AddDays(1));

            // ФИЛЬТР: Если НЕ "Show All", то показываем только свои
            if (!showAll)
            {
                query = query.Where(w => w.CoachId == user.Id);
            }

            var workouts = await query.OrderBy(w => w.StartTime).ToListAsync();

            return View(workouts);
        }

        // 2. СОЗДАТЬ ТРЕНИРОВКУ (Страница)
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // 3. СОЗДАТЬ ТРЕНИРОВКУ (Логика)
        [HttpPost]
        public async Task<IActionResult> Create(Workout workout)
        {
            var user = await _userManager.GetUserAsync(User);
            workout.CoachId = user.Id;
            workout.StartTime = DateTime.SpecifyKind(workout.StartTime, DateTimeKind.Utc);

            ModelState.Remove("Coach");
            ModelState.Remove("CoachId");

            if (ModelState.IsValid)
            {
                _context.Add(workout);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(workout);
        }

        // Детали тренировки
        public async Task<IActionResult> Details(int id)
        {
            var workout = await _context.Workouts
                .Include(w => w.Enrollments)
                .ThenInclude(e => e.Student)
                .FirstOrDefaultAsync(w => w.Id == id);

            if (workout == null) return NotFound();
            return View(workout);
        }

        // Посещаемость
        [HttpPost]
        public async Task<IActionResult> ToggleAttendance(int enrollmentId)
        {
            var enrollment = await _context.Enrollments.FindAsync(enrollmentId);
            if (enrollment != null)
            {
                enrollment.IsPresent = !enrollment.IsPresent;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Details", new { id = enrollment.WorkoutId });
        }

        // УДАЛЕНИЕ СТУДЕНТА (С УВЕДОМЛЕНИЕМ)
        [HttpPost]
        public async Task<IActionResult> RemoveStudent(int workoutId, string studentId)
        {
            var enrollment = await _context.Enrollments
                .Include(e => e.Workout)
                .FirstOrDefaultAsync(e => e.WorkoutId == workoutId && e.StudentId == studentId);

            if (enrollment != null)
            {
                // Уведомляем студента (История)
                var notif = new Notification
                {
                    UserId = studentId,
                    Message = $"Ваша запись на занятие '{enrollment.Workout.Title}' ({enrollment.Workout.StartTime.ToLocalTime():dd.MM}) была отменена тренером.",
                    CreatedAt = DateTime.UtcNow
                };
                _context.Notifications.Add(notif);

                _context.Enrollments.Remove(enrollment);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Details", new { id = workoutId });
        }

        // Удаление Тренировки
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var workout = await _context.Workouts
                .Include(w => w.Enrollments) // Подгружаем, чтобы уведомить
                .FirstOrDefaultAsync(w => w.Id == id);

            if (workout != null)
            {
                // Уведомляем всех записанных перед удалением
                foreach (var e in workout.Enrollments)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = e.StudentId,
                        Message = $"ВНИМАНИЕ: Тренировка '{workout.Title}' ({workout.StartTime.ToLocalTime():dd.MM HH:mm}) была ОТМЕНЕНА тренером.",
                        CreatedAt = DateTime.UtcNow
                    });
                }

                _context.Workouts.Remove(workout);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // Все тренировки (Gym Workouts) - С ФИЛЬТРАЦИЕЙ
        public async Task<IActionResult> AllWorkouts(bool showAll = true)
        {
            var user = await _userManager.GetUserAsync(User);

            var query = _context.Workouts
                .Include(w => w.Coach)
                .Include(w => w.Enrollments)
                .AsQueryable();

            // Если showAll = false, показываем только мои
            if (!showAll)
            {
                query = query.Where(w => w.CoachId == user.Id);
            }

            var allWorkouts = await query.OrderBy(w => w.StartTime).ToListAsync();

            ViewBag.ShowAll = showAll; // Передаем флаг в представление
            return View(allWorkouts);
        }

        // Список студентов
        [HttpGet]
        public async Task<IActionResult> Students()
        {
            // Берем студентов, которые записаны к этому тренеру
            var user = await _userManager.GetUserAsync(User);

            // Находим ID студентов через таблицу записей
            var studentIds = await _context.Enrollments
                .Include(e => e.Workout)
                .Where(e => e.Workout.CoachId == user.Id)
                .Select(e => e.StudentId)
                .Distinct()
                .ToListAsync();

            // Загружаем самих пользователей
            var students = await _userManager.Users
                .Where(u => studentIds.Contains(u.Id))
                .ToListAsync();

            return View(students);
        }

        // РЕДАКТИРОВАНИЕ (GET)
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var workout = await _context.Workouts.FindAsync(id);
            if (workout == null) return NotFound();
            return View(workout);
        }

        // РЕДАКТИРОВАНИЕ (POST) - С УВЕДОМЛЕНИЯМИ
        [HttpPost]
        public async Task<IActionResult> Edit(int id, Workout workout)
        {
            if (id != workout.Id) return NotFound();

            var originalWorkout = await _context.Workouts.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id);
            if (originalWorkout == null) return NotFound();

            workout.CoachId = originalWorkout.CoachId;
            workout.StartTime = DateTime.SpecifyKind(workout.StartTime, DateTimeKind.Utc);

            ModelState.Remove("Coach");
            ModelState.Remove("CoachId");

            if (ModelState.IsValid)
            {
                try
                {
                    // --- БЛОК УВЕДОМЛЕНИЙ ---
                    var enrollments = await _context.Enrollments
                        .Where(e => e.WorkoutId == workout.Id)
                        .Include(e => e.Student)
                        .ToListAsync();

                    foreach (var enrollment in enrollments)
                    {
                        var notif = new Notification
                        {
                            UserId = enrollment.StudentId,
                            Message = $"Изменения: Детали тренировки '{workout.Title}' были обновлены. Проверьте расписание.",
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.Notifications.Add(notif);
                    }
                    // -------------------------

                    _context.Update(workout);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Workouts.Any(e => e.Id == workout.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(workout);
        }

        // СТРАНИЦА: Написать сообщение
        [HttpGet]
        public async Task<IActionResult> SendMessage(string studentId)
        {
            var student = await _userManager.FindByIdAsync(studentId);
            if (student == null) return NotFound();

            ViewBag.StudentName = $"{student.FirstName} {student.LastName}";
            ViewBag.StudentId = studentId;
            return View();
        }

        // ОТПРАВИТЬ СООБЩЕНИЕ
        [HttpPost]
        public async Task<IActionResult> SendMessage(string studentId, string message)
        {
            var student = await _userManager.FindByIdAsync(studentId);
            if (student == null) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);

            var notification = new Notification
            {
                UserId = studentId,
                Message = $"Сообщение от тренера {currentUser.FirstName}: {message}",
                CreatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            return RedirectToAction("Students");
        }
    }
}