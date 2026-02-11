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
        // ТЗ: "Дашборд тренера" - показывает его тренировки
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            // Загружаем тренировки ТОЛЬКО этого тренера + список записавшихся
            var myWorkouts = await _context.Workouts
                .Include(w => w.Enrollments) // Подгружаем записи студентов
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

        // 3. СОЗДАТЬ ТРЕНИРОВКУ (Логика сохранения)
        [HttpPost]
        public async Task<IActionResult> Create(Workout workout)
        {
            var user = await _userManager.GetUserAsync(User);
            workout.CoachId = user.Id;

            // --- ВОТ ЭТО ИСПРАВЛЕНИЕ ---
            // PostgreSQL требует, чтобы дата была UTC. Мы ставим эту метку вручную.
            workout.StartTime = DateTime.SpecifyKind(workout.StartTime, DateTimeKind.Utc);
            // ---------------------------

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
        // Просмотр деталей тренировки и списка записавшихся
        public async Task<IActionResult> Details(int id)
        {
            var workout = await _context.Workouts
                .Include(w => w.Enrollments)
                .ThenInclude(e => e.Student) // Важно: подгружаем данные студентов (Имя, Фамилия)
                .FirstOrDefaultAsync(w => w.Id == id);

            if (workout == null) return NotFound();

            // Проверка безопасности: только создатель может смотреть (опционально)
            // var user = await _userManager.GetUserAsync(User);
            // if (workout.CoachId != user.Id) return Forbid();

            return View(workout);
        }
        // ДЕЙСТВИЕ: Отметить посещение (Пришел / Не пришел)
        [HttpPost]
        public async Task<IActionResult> ToggleAttendance(int enrollmentId)
        {
            var enrollment = await _context.Enrollments.FindAsync(enrollmentId);

            if (enrollment != null)
            {
                // Меняем статус на противоположный (true <-> false)
                enrollment.IsPresent = !enrollment.IsPresent;
                await _context.SaveChangesAsync();
            }

            // Возвращаем тренера обратно на страницу списка
            return RedirectToAction("Details", new { id = enrollment.WorkoutId });
        }

        // ДЕЙСТВИЕ: Удалить тренировку (Мы обещали это доделать)
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
        // СТРАНИЦА: Все тренировки в зале (для просмотра загруженности)
        public async Task<IActionResult> AllWorkouts()
        {
            var user = await _userManager.GetUserAsync(User);

            var allWorkouts = await _context.Workouts
                .Include(w => w.Coach)       // Чтобы видеть имя другого тренера
                .Include(w => w.Enrollments) // Чтобы видеть, сколько людей записано
                .OrderBy(w => w.StartTime)
                .ToListAsync();

            return View(allWorkouts);
        }
        // СТРАНИЦА: Список всех студентов
        [HttpGet]
        public async Task<IActionResult> Students()
        {
            // Получаем список всех пользователей, у которых роль "Student"
            var students = await _userManager.GetUsersInRoleAsync("Student");
            return View(students);
        }
        // РЕДАКТИРОВАНИЕ: Открыть форму (GET)
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var workout = await _context.Workouts.FindAsync(id);
            if (workout == null) return NotFound();

            // Проверка: редактировать может только создатель (опционально)
            // var user = await _userManager.GetUserAsync(User);
            // if (workout.CoachId != user.Id) return Forbid();

            return View(workout);
        }

        // РЕДАКТИРОВАНИЕ: Сохранить изменения (POST)
        [HttpPost]
        public async Task<IActionResult> Edit(int id, Workout workout)
        {
            if (id != workout.Id) return NotFound();

            // Нам нужно сохранить CoachId, так как форма его не передает
            // Поэтому сначала достаем оригинал из базы, чтобы не потерять тренера
            var originalWorkout = await _context.Workouts.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id);

            if (originalWorkout == null) return NotFound();

            // Восстанавливаем ID тренера
            workout.CoachId = originalWorkout.CoachId;

            // Фикс даты для PostgreSQL (снова UTC)
            workout.StartTime = DateTime.SpecifyKind(workout.StartTime, DateTimeKind.Utc);

            // Убираем валидацию тренера
            ModelState.Remove("Coach");
            ModelState.Remove("CoachId");

            if (ModelState.IsValid)
            {
                try
                {
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
    }
}