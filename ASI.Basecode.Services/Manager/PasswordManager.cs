using Microsoft.AspNetCore.Identity;

namespace ASI.Basecode.Services.Manager
{
    /// <summary>
    /// Password and lockout rules for every CampusGear account.
    /// Passwords are hashed and verified by ASP.NET Identity; this class only sets the rules it enforces.
    /// </summary>
    public static class PasswordManager
    {
        public const int RequiredLength = 8;
        // WBS 5.00 | These settings apply to Identity's per-account failed-password counter.
        public const int MaxFailedAccessAttempts = 5;
        public static readonly TimeSpan LockoutTimeSpan = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Applies the password and lockout rules to the Identity options.
        /// Identity's defaults also require an upper-case letter, a lower-case letter, a digit and a symbol.
        /// </summary>
        public static void SetUp(IdentityOptions options)
        {
            options.Password.RequiredLength = RequiredLength;
            options.Lockout.DefaultLockoutTimeSpan = LockoutTimeSpan;
            options.Lockout.MaxFailedAccessAttempts = MaxFailedAccessAttempts;
            options.Lockout.AllowedForNewUsers = true;
        }
    }
}
