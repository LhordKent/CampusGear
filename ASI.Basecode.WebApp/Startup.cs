using System.Threading.RateLimiting;
using ASI.Basecode.Data;
using ASI.Basecode.WebApp.Configuration;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.WebApp
{
    // Application configuration (services and HTTP request pipeline)
    internal partial class StartupConfigurer
    {
        private IConfiguration Configuration { get; }

        private IApplicationBuilder _app = null!;

        private readonly IWebHostEnvironment _environment;

        private IServiceCollection _services = null!;

        private readonly Configuration.ConfigurationExtensions.SecretsLoadStatus _developmentSecrets;

        /// <summary>
        /// Initializes a new instance of the StartupConfigurer class.
        /// </summary>
        /// <param name="configuration">Application configuration</param>
        /// <param name="environment">Hosting environment</param>
        /// <param name="args">Command-line arguments</param>
        public StartupConfigurer(ConfigurationManager configuration, IWebHostEnvironment environment, string[] args)
        {
            this.Configuration = configuration;
            this._environment = environment;
            this._developmentSecrets = configuration.LoadProjectSecrets(environment, args);
        }

        /// <summary>
        /// Registers the application services.
        /// </summary>
        public void ConfigureServices(IServiceCollection services)
        {
            this._services = services;

            services.AddControllersWithViews();
            services.AddHttpContextAccessor();

            // Register SQL database configuration context as services.
            services.AddDbContext<AsiBasecodeDBContext>(options =>
                options.UseSqlServer(this.Configuration.GetCampusGearConnectionString())
            );

            // Session (account flows: administrator code, password reset)
            services.AddDistributedMemoryCache();
            services.AddSession(options =>
            {
                options.Cookie.Name = "CampusGear.Flow";
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = this._environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
                options.IdleTimeout = TimeSpan.FromMinutes(15);
            });

            // Sign-in, sign-up and password-reset posts are limited per client address.
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddPolicy(
                    "auth-post",
                    context =>
                        RateLimitPartition.GetFixedWindowLimiter(
                            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                            _ => new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = 20,
                                Window = TimeSpan.FromMinutes(1),
                                QueueLimit = 0,
                            }
                        )
                );
            });

            // DI Services AutoMapper(Add Profile)
            this.ConfigureAutoMapper();

            // DI Services
            this.ConfigureOtherServices();

            // Email senders
            this.ConfigureEmail();

            // Authentication and Authorization
            this.ConfigureAuthorization();
        }

        /// <summary>
        /// Configures the HTTP request pipeline.
        /// </summary>
        public void ConfigureApp(IApplicationBuilder app)
        {
            this._app = app;

            this.ConfigureLogger();

            if (!this._environment.IsDevelopment())
            {
                this._app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                this._app.UseHsts();
            }

            this._app.UseStatusCodePagesWithReExecute("/status/{0}");
            this._app.UseHttpsRedirection();
            this._app.UseStaticFiles();
            this._app.UseRouting();
            this._app.UseRateLimiter();
            this._app.UseSession();

            this._app.UseAuthentication();
            this._app.UseAuthorization();
        }
    }
}
