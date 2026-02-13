using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UniFitApp.Data;
using UniFitApp.Models;

var builder = WebApplication.CreateBuilder(args);

// Добавляем сервисы (MVC)
builder.Services.AddControllersWithViews();

// --- ПОДКЛЮЧЕНИЕ POSTGRESQL ---
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));
// ------------------------------
builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false; // Упростим пароли для тестов
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
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication(); // <-- Добавить эту строку (Проверка кто это)
app.UseStaticFiles();
app.UseAuthorization();  // <-- Эта уже была (Проверка прав доступа)

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    // Список ролей, которые нам нужны по ТЗ
    string[] roleNames = { "Student", "Coach" };

    foreach (var roleName in roleNames)
    {
        // Если роли нет - создаем её
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }
}
app.Run();