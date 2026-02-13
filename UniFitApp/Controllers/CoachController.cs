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
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var myWorkouts = await _context.Workouts
                .Include(w => w.Enrollments)
                .Where(w => w.CoachId == user.Id)
                .OrderBy(w => w.StartTime)
                .ToListAsync();

            return View(myWorkouts);
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

        // Удаление
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var workout = await _context.Workouts.FindAsync(id);
            if (workout != null)
            {
                _context.Workouts.Remove(workout);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // Все тренировки
        public async Task<IActionResult> AllWorkouts()
        {
            var allWorkouts = await _context.Workouts
                .Include(w => w.Coach)
                .Include(w => w.Enrollments)
                .OrderBy(w => w.StartTime)
                .ToListAsync();
            return View(allWorkouts);
        }

        // Список студентов
        [HttpGet]
        public async Task<IActionResult> Students()
        {
            var students = await _userManager.GetUsersInRoleAsync("Student");
            // Нужно загрузить фото вручную, так как GetUsersInRoleAsync может не подтянуть все поля (зависит от настроек Identity)
            // Но обычно IdentityUser загружается полностью. 
            // Для надежности можно перебрать и догрузить, но пока оставим так.
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
                    // 1. Ищем всех студентов, записанных на эту тренировку
                    var enrollments = await _context.Enrollments
                        .Where(e => e.WorkoutId == workout.Id)
                        .Include(e => e.Student)
                        .ToListAsync();

                    // 2. Отправляем каждому уведомление
                    foreach (var enrollment in enrollments)
                    {
                        var notif = new Notification
                        {
                            UserId = enrollment.StudentId,
                            Message = $"Внимание! Тренировка '{workout.Title}' была изменена. Новое время: {workout.StartTime:dd.MM HH:mm}. Проверьте детали.",
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