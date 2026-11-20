using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.AspNetCore.Http;

namespace ASI.Basecode.Services.Manager
{
    /// <summary>
    /// Keeps the in-progress account flows (administrator sign-in code, password reset) in the HTTP session.
    /// </summary>
    public sealed class SessionManager : IAccountFlowStore
    {
        private readonly IHttpContextAccessor _accessor;

        public SessionManager(IHttpContextAccessor accessor)
        {
            _accessor = accessor;
        }

        private ISession Session =>
            _accessor.HttpContext?.Session ?? throw new InvalidOperationException("An account flow requires an HTTP session.");

        // WBS 4.00 / WBS 5.00 | Carry the account ID and stamp between the password and code requests.
        public AccountChallengeState? PendingAdministrator
        {
            get { return ReadChallenge("CampusGear.PendingAdministratorUserId", "CampusGear.PendingAdministratorStamp"); }
            set { WriteChallenge("CampusGear.PendingAdministratorUserId", "CampusGear.PendingAdministratorStamp", value); }
        }

        public AccountChallengeState? PendingPasswordReset
        {
            get { return ReadChallenge("CampusGear.PendingResetUserId", "CampusGear.PendingResetStamp"); }
            set { WriteChallenge("CampusGear.PendingResetUserId", "CampusGear.PendingResetStamp", value); }
        }

        public PasswordResetAuthorization? AuthorizedPasswordReset
        {
            get
            {
                var id = Session.GetString("CampusGear.AuthorizedResetUserId");
                if (id is null)
                {
                    return null;
                }

                long.TryParse(
                    Session.GetString("CampusGear.AuthorizedResetExpiresAt"),
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var expiresAt
                );
                return new(id, Session.GetString("CampusGear.AuthorizedResetStamp") ?? "", expiresAt);
            }
            set
            {
                WriteChallenge(
                    "CampusGear.AuthorizedResetUserId",
                    "CampusGear.AuthorizedResetStamp",
                    value is null ? null : new(value.UserId, value.SecurityStamp)
                );
                if (value is null)
                {
                    Session.Remove("CampusGear.AuthorizedResetExpiresAt");
                }
                else
                {
                    Session.SetString(
                        "CampusGear.AuthorizedResetExpiresAt",
                        value.ExpiresAtUnixSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    );
                }
            }
        }

        private AccountChallengeState? ReadChallenge(string userKey, string stampKey)
        {
            var id = Session.GetString(userKey);
            return id is null ? null : new(id, Session.GetString(stampKey) ?? "");
        }

        private void WriteChallenge(string userKey, string stampKey, AccountChallengeState? value)
        {
            if (value is null)
            {
                Session.Remove(userKey);
                Session.Remove(stampKey);
                return;
            }

            Session.SetString(userKey, value.UserId);
            Session.SetString(stampKey, value.SecurityStamp);
        }

        // WBS 4.00 / WBS 5.00 | Logout discards pending sign-in and reset state.
        public void Clear() => Session.Clear();
    }
}
