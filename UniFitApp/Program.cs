using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UniFitApp.Data;
using UniFitApp.Models;

var builder = WebApplication.CreateBuilder(args);

// === ДОБАВЛЕНО: Увеличение лимита для загрузки больших фото с телефона ===
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 52428800; // 50 МБ
});
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 52428800; // 50 МБ
});
// =========================================================================

// Добавляем сервисы (MVC)
builder.Services.AddControllersWithViews();

// --- ПОДКЛЮЧЕНИЕ POSTGRESQL ---
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));
// ------------------------------

builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 4;
})
.AddEntityFrameworkStores<ApplicationDbContext>();

var app = builder.Build();

// Настройка HTTP-конвейера
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // Оставляем только один вызов здесь

app.UseRouting();

app.UseAuthentication(); // Проверка кто это
app.UseAuthorization();  // Проверка прав доступа

// Маршрут по умолчанию: Сначала Login.
// А Login (Get) сам перекинет на Welcome, если юзер уже вошел.
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

// Создание ролей и АДМИНА при старте
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();

    // Применяем миграции
    context.Database.Migrate();

    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = services.GetRequiredService<UserManager<AppUser>>();

    // === ДОБАВИЛИ РОЛЬ ADMIN ===
    string[] roleNames = { "Student", "Coach", "Admin" };

    foreach (var roleName in roleNames)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }

    // === СОЗДАЕМ СУПЕР-АДМИНА ===
    if (await userManager.FindByEmailAsync("admin@unifit.com") == null)
    {
        var admin = new AppUser
        {
            UserName = "admin@unifit.com",
            Email = "admin@unifit.com",
            FirstName = "Super",
            LastName = "Admin"
        };
        // Пароль: admin
        var createPowerUser = await userManager.CreateAsync(admin, "admin");
        if (createPowerUser.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, "Admin");
        }
    }
}

app.Run();