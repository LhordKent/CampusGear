using CampusGear.Data;
using CampusGear.Models;
using CampusGear.Services;
using CampusGear.Services.Reservations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

var developmentSecrets = DevelopmentConfiguration.LoadProjectSecrets(builder.Configuration, builder.Environment, args);

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages(); // The Figma reference demo remains under /demo.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("CampusGear")
        ?? throw new InvalidOperationException("ConnectionStrings:CampusGear is required.")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = true;
        options.Password.RequiredLength = 8;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/auth/login";
    options.AccessDeniedPath = "/auth/access-denied";
    options.Cookie.Name = "CampusGear.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
});
builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "CampusGear.Flow";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.IdleTimeout = TimeSpan.FromMinutes(15);
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth-post", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});
builder.Services.AddScoped<EmailChallengeService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IReservationService, ReservationService>();
builder.Services.AddHostedService<ReservationExpiryWorker>();
// The in-memory mailbox must be explicitly selected; it never delivers to an inbox.
var emailProvider = builder.Configuration["Email:Provider"] ?? "Smtp";
if (emailProvider.Equals("Development", StringComparison.OrdinalIgnoreCase))
{
    if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException("The Development email provider is available only in Development.");
    builder.Services.AddSingleton<DevelopmentEmailSender>();
    builder.Services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<DevelopmentEmailSender>());
}
else if (emailProvider.Equals("Smtp", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
}
else
{
    throw new InvalidOperationException("Email:Provider must be Development or Smtp.");
}

var app = builder.Build();

app.Logger.LogInformation("Email provider: {EmailProvider}; environment: {Environment}",
    emailProvider, app.Environment.EnvironmentName);
if (emailProvider.Equals("Smtp", StringComparison.OrdinalIgnoreCase))
    app.Logger.LogInformation("SMTP startup configuration: Host configured={HostConfigured}; From configured={FromConfigured}.",
        !string.IsNullOrWhiteSpace(builder.Configuration["Email:Smtp:Host"]),
        !string.IsNullOrWhiteSpace(builder.Configuration["Email:Smtp:From"]));
if (app.Environment.IsDevelopment())
    app.Logger.LogInformation("Shared Development email settings: file found={FileFound}.", developmentSecrets.FileFound);
if (emailProvider.Equals("Development", StringComparison.OrdinalIgnoreCase))
    app.Logger.LogWarning("The Development email provider stores messages in memory and does not send email. Select Smtp for inbox delivery.");

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseRateLimiter();
app.UseSession();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var users = context.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.GetUserAsync(context.User);
        if (user is null || !user.IsActive)
        {
            await context.SignOutAsync(IdentityConstants.ApplicationScheme);
            context.User = new System.Security.Claims.ClaimsPrincipal();
        }
    }
    await next();
});
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();

await DbInitializer.InitializeAsync(app.Services, app.Environment.IsDevelopment());
if (args.Contains("--initialize-only", StringComparer.OrdinalIgnoreCase)) return;
app.Run();
