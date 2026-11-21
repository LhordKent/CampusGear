using System.Security.Claims;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace ASI.Basecode.WebApp.Authentication
{
    /// <summary>
    /// CampusGear's sign-in manager: ASP.NET Identity's cookie sign-in, plus the rule that only an
    /// active, administrator-approved account may keep a signed-in session.
    /// </summary>
    public sealed class SignInManager : SignInManager<User>
    {
        public SignInManager(
            UserManager<User> userManager,
            IHttpContextAccessor contextAccessor,
            IUserClaimsPrincipalFactory<User> claimsFactory,
            IOptions<IdentityOptions> optionsAccessor,
            ILogger<SignInManager<User>> logger,
            IAuthenticationSchemeProvider schemes,
            IUserConfirmation<User> confirmation
        )
            : base(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation) { }

        /// <summary>
        /// WBS 5.00 | Runs for the sign-in cookie on every request (validation interval is zero in Startup.Auth.cs).
        /// Returning null rejects the cookie and signs the user out, so a deleted, deactivated or
        /// unapproved account is signed out on its next request.
        /// </summary>
        public override async Task<User?> ValidateSecurityStampAsync(ClaimsPrincipal? principal)
        {
            var user = await base.ValidateSecurityStampAsync(principal);
            return user is { IsActive: true, ApprovalStatus: AccountApprovalStatus.Approved } ? user : null;
        }
    }
}
