using CampusGear.Services.ServiceModels;

namespace CampusGear.Services.Interfaces
{
    // Session transport is implemented in WebApp. A missing state and a state with
    // an empty UserId (an unknown reset email) deliberately have different meanings.
    public interface IAccountFlowStore
    {
        AccountChallengeState? PendingAdministrator { get; set; }

        AccountChallengeState? PendingPasswordReset { get; set; }

        PasswordResetAuthorization? AuthorizedPasswordReset { get; set; }

        void Clear();
    }
}
