using System.Security.Claims;
using Clinic.Web.Data;
using Clinic.Web.Services;
using Clinic.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllersWithViews()
    .AddNewtonsoftJson(options =>
        options.SerializerSettings.ReferenceLoopHandling =
            Newtonsoft.Json.ReferenceLoopHandling.Ignore);

builder.Services.AddDbContext<ClinicContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("ClinicConn"));
    // Suppress pending model changes warning - migrations will be applied explicitly
    options.ConfigureWarnings(warnings => 
        warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
});

builder.Services.AddScoped<IAppointmentService, AppointmentService>();

// Authentication: Cookie scheme
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
        options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;
        // for dev, keep defaults; consider sliding expiration, etc.
    });

// Authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

var app = builder.Build();

// DB Migration + Seed (Dev only)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ClinicContext>();
    try
    {
        // Check if database exists and get pending migrations
        var pendingMigrations = db.Database.GetPendingMigrations().ToList();
        if (pendingMigrations.Any())
        {
            Console.WriteLine($"Applying {pendingMigrations.Count} pending migration(s): {string.Join(", ", pendingMigrations)}");
            db.Database.Migrate();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Migration error: {ex.Message}");
        // In development, try to create database if it doesn't exist
        if (app.Environment.IsDevelopment())
        {
            try
            {
                db.Database.EnsureCreated();
                Console.WriteLine("Database created using EnsureCreated()");
            }
            catch (Exception ex2)
            {
                Console.WriteLine($"Failed to create database: {ex2.Message}");
                throw;
            }
        }
        else
        {
            throw;
        }
    }

    if (!db.Doctors.Any())
    {
        db.Doctors.AddRange(
            new Doctor { Name = "Dr. Rajesh Kumar", Specialization = "General Physician" },
            new Doctor { Name = "Dr. Meera Rao", Specialization = "ENT" },
            new Doctor { Name = "Dr. Anil Verma", Specialization = "Pediatrics" }
        );
        db.SaveChanges();
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Authentication must come before Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();

app.Run();
