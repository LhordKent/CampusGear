using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.WebApp.Authentication;
using Microsoft.AspNetCore.Identity;

// AUTH SETUP  -  ASP.NET Identity, the password rules (PasswordManager), and the sign-in cookie settings.
namespace ASI.Basecode.WebApp
{
    // Authorization configuration
    internal partial class StartupConfigurer
    {
        private void ConfigureAuthorization()
        {
            this._services
                .AddIdentity<User, IdentityRole>(options =>
                {
                    options.User.RequireUniqueEmail = true;
                    options.SignIn.RequireConfirmedEmail = true;
                    PasswordManager.SetUp(options);
                })
                .AddEntityFrameworkStores<AsiBasecodeDBContext>()
                .AddSignInManager<SignInManager>()
                .AddDefaultTokenProviders();

            this._services.ConfigureApplicationCookie(options =>
            {
                options.LoginPath = "/auth/login";
                options.AccessDeniedPath = "/auth/access-denied";
                options.Cookie.Name = "CampusGear.Auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = this._environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                // Not signed in: a browser page visit goes to the login page; fetch calls and form posts get 401.
                // Signed in with the wrong role: always 403 (HomeController renders the access-denied page for browsers).
                options.Events.OnRedirectToLogin = context =>
                {
                    if (AuthResponses.IsPageNavigation(context.Request))
                    {
                        context.Response.Redirect(context.RedirectUri);
                    }
                    else
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    }

                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

            // The sign-in cookie is re-checked on every request (see Authentication/SignInManager.cs), so a
            // deactivated or unapproved account loses its open session immediately.
            this._services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
        }
    }
}
