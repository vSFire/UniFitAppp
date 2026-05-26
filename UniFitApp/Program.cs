using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UniFitApp.Data;
using UniFitApp.Models;

var builder = WebApplication.CreateBuilder(args);


builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 52428800; 
});
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 52428800; 
});



builder.Services.AddControllersWithViews();


var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true; // Обязательно минимум 1 цифра
    options.Password.RequireLowercase = true; // Обязательно маленькая буква
    options.Password.RequireUppercase = true; // Обязательно БОЛЬШАЯ буква
    options.Password.RequireNonAlphanumeric = true; // Обязательно спецсимвол (например, @, !, #)
    options.Password.RequiredLength = 6; // Минимальная длина пароля (сделаем 6 вместо 4)
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddErrorDescriber<RussianIdentityErrorDescriber>()
.AddDefaultTokenProviders();

builder.Services.AddTransient<UniFitApp.Services.EmailService>();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    context.Database.Migrate(); // Эта магия сама добавит поле ImageUrl в базу
}
// Настройка HTTP-конвейера
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // Оставляем только один вызов здесь
// === ГЛОБАЛЬНАЯ ЛОКАЛИЗАЦИЯ (РУССКИЕ ДАТЫ) ===
var supportedCultures = new[] { "ru-RU" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture(supportedCultures[0])
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

app.UseRequestLocalization(localizationOptions);
// =============================================
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
        var createPowerUser = await userManager.CreateAsync(admin, "Admin@2026");
        if (createPowerUser.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, "Admin");
        }
    }
}

app.Run();