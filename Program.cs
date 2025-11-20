using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Identity;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using System.Threading;
using System;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        // Fix circular reference issues in JSON serialization
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// Add DbContext with Identity
builder.Services.AddDbContext<CarRentalDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection") 
        ?? "Server=(localdb)\\mssqllocaldb;Database=CarRentalDb;Trusted_Connection=True;MultipleActiveResultSets=true"));

// Add Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;

    // User settings
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = true; // Enable email confirmation

    // Lockout settings - Login Blocking
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(30); // Lock for 30 minutes
    options.Lockout.MaxFailedAccessAttempts = 5; // Lock after 5 failed attempts
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<CarRentalDbContext>()
.AddDefaultTokenProviders();

// Configure cookie settings
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
});

// Add custom services
builder.Services.AddScoped<IAppointmentService, AppointmentService>();
builder.Services.AddScoped<IPromotionService, PromotionService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IFileUploadService, FileUploadService>();
builder.Services.AddScoped<ICaptchaService, CaptchaService>();

// Add Session for CAPTCHA
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(10);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
// Add detailed error page for development (must be first)
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseSession();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Initialize database asynchronously in background (don't block startup)
_ = Task.Run(async () =>
{
    try
    {
        // Wait for app to be fully ready
        await Task.Delay(5000);
        
        try
        {
            using var scope = app.Services.CreateScope();
            var services = scope.ServiceProvider;
            var context = services.GetRequiredService<CarRentalDbContext>();
            var loggerFactory = services.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("DatabaseInitialization");
            
            try
            {
                logger.LogInformation("Starting database initialization...");
                
                // Ensure database exists
                await context.Database.EnsureCreatedAsync();
                logger.LogInformation("Database ensured.");
                
                // Try to create missing tables if needed (with timeout)
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    var connection = context.Database.GetDbConnection();
                    
                    if (connection.State != System.Data.ConnectionState.Open)
                    {
                        await connection.OpenAsync(cts.Token);
                    }
                    
                    using var command = connection.CreateCommand();
                    command.CommandTimeout = 30;
                    command.CommandText = @"
                        IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Favorites]') AND type in (N'U'))
                        BEGIN
                            CREATE TABLE [dbo].[Favorites] (
                                [Id] int NOT NULL IDENTITY,
                                [UserId] nvarchar(450) NOT NULL,
                                [CarId] int NOT NULL,
                                [CreatedAt] datetime2 NOT NULL,
                                CONSTRAINT [PK_Favorites] PRIMARY KEY ([Id]),
                                CONSTRAINT [FK_Favorites_Cars_CarId] FOREIGN KEY ([CarId]) REFERENCES [dbo].[Cars] ([Id]) ON DELETE CASCADE,
                                CONSTRAINT [FK_Favorites_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
                            );
                            CREATE UNIQUE INDEX [IX_Favorites_UserId_CarId] ON [dbo].[Favorites] ([UserId], [CarId]);
                        END
                        IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Notifications]') AND type in (N'U'))
                        BEGIN
                            CREATE TABLE [dbo].[Notifications] (
                                [Id] int NOT NULL IDENTITY,
                                [UserId] nvarchar(450) NOT NULL,
                                [Title] nvarchar(max) NOT NULL,
                                [Message] nvarchar(max) NOT NULL,
                                [Type] nvarchar(max) NULL,
                                [IsRead] bit NOT NULL,
                                [CreatedAt] datetime2 NOT NULL,
                                CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id]),
                                CONSTRAINT [FK_Notifications_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
                            );
                            CREATE INDEX [IX_Notifications_UserId] ON [dbo].[Notifications] ([UserId]);
                        END
                        IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Categories]') AND type in (N'U'))
                        BEGIN
                            CREATE TABLE [dbo].[Categories] (
                                [Id] int NOT NULL IDENTITY,
                                [Name] nvarchar(max) NOT NULL,
                                [Description] nvarchar(max) NULL,
                                [IsActive] bit NOT NULL,
                                CONSTRAINT [PK_Categories] PRIMARY KEY ([Id])
                            );
                            CREATE UNIQUE INDEX [IX_Categories_Name] ON [dbo].[Categories] ([Name]) WHERE [Name] IS NOT NULL;
                        END
                    ";
                    await command.ExecuteNonQueryAsync(cts.Token);
                    
                    // Seed data if available
                    try
                    {
                        await SeedData.InitializeAsync(services);
                        logger.LogInformation("Database seeded successfully.");
                    }
                    catch (Exception seedEx)
                    {
                        logger.LogWarning(seedEx, "Could not seed database: {Message}", seedEx.Message);
                    }
                }
                catch (Exception dbEx)
                {
                    logger.LogWarning(dbEx, "Database table creation skipped: {Message}", dbEx.Message);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error initializing database: {Message}", ex.Message);
            }
        }
        catch (Exception scopeEx)
        {
            Console.WriteLine($"[DatabaseInit] Service scope error: {scopeEx.Message}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[DatabaseInit] Background task error: {ex.Message}");
    }
});

// Start the application - this blocks until shutdown
try
{
    Console.WriteLine("Application starting...");
    app.Run();
}
catch (Exception ex)
{
    Console.WriteLine($"FATAL ERROR: Application crashed: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
    Environment.Exit(1);
}

