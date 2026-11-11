using System.Net;
using IncidentManagementSystem.Authorization;
using IncidentManagementSystem.Data;
using IncidentManagementSystem.Models.Identity;
using IncidentManagementSystem.Services.Implementations;
using IncidentManagementSystem.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' was not found. Configure it in appsettings.json or User Secrets.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 12;
        options.Password.RequiredUniqueChars = 4;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthorization(options =>
{
    foreach (var permission in Permissions.All)
    {
        options.AddPolicy(permission, policy =>
            policy.RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission)));
    }
});

builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddScoped<IClaimsTransformation, PermissionClaimsTransformation>();
builder.Services.AddScoped<PermissionSeeder>();
builder.Services.AddScoped<InitialAdminSeeder>();
builder.Services.AddScoped<ICurrentUserContext, CurrentUserContext>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IDepartmentService, DepartmentService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IIncidentService, IncidentService>();
builder.Services.AddScoped<IIncidentDepartmentService, IncidentDepartmentService>();
builder.Services.AddScoped<IIncidentNotificationService, IncidentNotificationService>();
builder.Services.AddScoped<IIncidentTransferService, IncidentTransferService>();
builder.Services.AddScoped<IIncidentReviewService, IncidentReviewService>();
builder.Services.AddScoped<IShiftScheduleService, ShiftScheduleService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IIncidentReportService, IncidentReportService>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "IncidentManagementSystem.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = true;
});

builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.FromMinutes(5));

builder.Services.AddAutoMapper(cfg => { }, typeof(Program).Assembly);

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var permissionSeeder = scope.ServiceProvider.GetRequiredService<PermissionSeeder>();
    await permissionSeeder.SeedAsync();
    var initialAdminSeeder = scope.ServiceProvider.GetRequiredService<InitialAdminSeeder>();
    await initialAdminSeeder.SeedAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health/database", async (ApplicationDbContext dbContext, HttpContext context, ILogger<Program> logger) =>
{
    if (context.Connection.RemoteIpAddress is not { } remoteAddress || !IPAddress.IsLoopback(remoteAddress))
    {
        return Results.NotFound();
    }

    try
    {
        return await dbContext.Database.CanConnectAsync(context.RequestAborted)
            ? Results.Ok("Database connection is available.")
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "The configured SQL Server database is unavailable.");
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
})
    .AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
