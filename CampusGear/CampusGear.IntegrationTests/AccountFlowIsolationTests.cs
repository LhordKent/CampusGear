using CampusGear.Data;
using CampusGear.Data.Models;
using CampusGear.Resources.Constants;
using CampusGear.Services.Interfaces;
using CampusGear.Services.ServiceModels;
using CampusGear.Services.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CampusGear.IntegrationTests
{
    [Collection("Web app permissions")]
    public sealed class AccountFlowIsolationTests(WebAppFixture app)
    {
        private static readonly RequestContext Actor = new(null, "127.0.0.9", "flow-characterization");

        private static PasswordResetService Service(IServiceProvider provider, MemoryFlow flow)
        {
            return ActivatorUtilities.CreateInstance<PasswordResetService>(provider, flow);
        }

        [Fact]
        public async Task Unknown_reset_email_keeps_empty_marker_distinct_from_missing_flow()
        {
            await using var scope = app.Services.CreateAsyncScope();
            var flow = new MemoryFlow();
            var service = Service(scope.ServiceProvider, flow);
            Assert.Equal(PasswordResetStatus.RequestRequired, (await service.GetPasswordResetCodeAsync(Actor)).Value!.Status);
            var result = await service.PasswordResetRequestAsync(Actor, Guid.NewGuid() + "@unknown.test");
            Assert.Equal(PasswordResetStatus.CodeRequired, result.Value!.Status);
            Assert.Equal("", flow.GetString("CampusGear.PendingResetUserId"));
            Assert.Equal(PasswordResetStatus.CodeRequired, (await service.GetPasswordResetCodeAsync(Actor)).Value!.Status);
            var rejected = await service.PasswordResetCodeAsync(Actor, "000000");
            Assert.Equal(PasswordResetStatus.CodeRequired, rejected.Value!.Status);
            Assert.Equal("The code is invalid or has expired.", rejected.Message);
        }

        [Fact]
        public async Task Reset_stamp_change_revokes_flow_before_code_is_consumed_and_records_context()
        {
            var account = await app.NewAccountAsync(RoleNames.Borrower);
            await using var scope = app.Services.CreateAsyncScope();
            var flow = new MemoryFlow();
            var service = Service(scope.ServiceProvider, flow);
            await service.PasswordResetRequestAsync(Actor, account.Email);
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            await users.UpdateSecurityStampAsync((await users.FindByIdAsync(account.UserId))!);
            var result = await service.PasswordResetCodeAsync(Actor, "000000");
            Assert.Equal(PasswordResetStatus.RequestRequired, result.Value!.Status);
            Assert.False(result.Succeeded);
            Assert.Null(flow.GetString("CampusGear.PendingResetUserId"));
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var audit = await db.AuditEvents.SingleAsync(x => x.EntityId == account.UserId && x.Action == "Auth.PasswordResetCode");
            Assert.Equal("StaleFlow", audit.Outcome);
            Assert.Null(audit.ActorUserId);
            Assert.Equal(Actor.IpAddress, audit.IpAddress);
            Assert.Equal(Actor.CorrelationId, audit.CorrelationId);
            Assert.Null((await db.EmailChallenges.SingleAsync(x => x.UserId == account.UserId)).ConsumedAtUtc);
        }

        [Fact]
        public async Task Expired_reset_permission_clears_all_authorized_keys()
        {
            var flow = new MemoryFlow();
            flow.SetString("CampusGear.AuthorizedResetUserId", "expired-user");
            flow.SetString("CampusGear.AuthorizedResetStamp", "stamp");
            flow.SetString("CampusGear.AuthorizedResetExpiresAt", DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds().ToString());
            await using var scope = app.Services.CreateAsyncScope();
            var result = await Service(scope.ServiceProvider, flow).GetPasswordResetNewAsync(Actor);
            Assert.Equal(PasswordResetStatus.RequestRequired, result.Value!.Status);
            Assert.Equal("Your password reset permission expired. Request a new code.", result.Message);
            Assert.Empty(flow.Values);
        }

        [Fact]
        public async Task Administrator_stamp_change_clears_pending_sign_in()
        {
            var account = await app.NewAccountAsync(RoleNames.Administrator);
            await using var scope = app.Services.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByIdAsync(account.UserId))!;
            var flow = new MemoryFlow();
            flow.SetString("CampusGear.PendingAdministratorUserId", user.Id);
            flow.SetString("CampusGear.PendingAdministratorStamp", user.SecurityStamp!);
            await users.UpdateSecurityStampAsync(user);
            var result = await ActivatorUtilities
                .CreateInstance<AuthenticationService>(scope.ServiceProvider, flow)
                .GetTwoFactorAsync(Actor);
            Assert.Equal(AuthenticationStatus.CredentialsRequired, result.Value!.Status);
            Assert.False(result.Succeeded);
            Assert.Equal("This sign-in request is no longer valid. Sign in again.", result.Message);
            Assert.Empty(flow.Values);
        }

        private sealed class MemoryFlow : IAccountFlowStore
        {
            public Dictionary<string, string> Values { get; } = [];

            public AccountChallengeState? PendingAdministrator
            {
                get { return Read("CampusGear.PendingAdministratorUserId", "CampusGear.PendingAdministratorStamp"); }
                set { Write("CampusGear.PendingAdministratorUserId", "CampusGear.PendingAdministratorStamp", value); }
            }

            public AccountChallengeState? PendingPasswordReset
            {
                get { return Read("CampusGear.PendingResetUserId", "CampusGear.PendingResetStamp"); }
                set { Write("CampusGear.PendingResetUserId", "CampusGear.PendingResetStamp", value); }
            }

            public PasswordResetAuthorization? AuthorizedPasswordReset
            {
                get
                {
                    var state = Read("CampusGear.AuthorizedResetUserId", "CampusGear.AuthorizedResetStamp");
                    long.TryParse(GetString("CampusGear.AuthorizedResetExpiresAt"), out var expiresAt);
                    return state is null ? null : new(state.UserId, state.SecurityStamp, expiresAt);
                }
                set
                {
                    Write(
                        "CampusGear.AuthorizedResetUserId",
                        "CampusGear.AuthorizedResetStamp",
                        value is null ? null : new(value.UserId, value.SecurityStamp)
                    );
                    if (value is null)
                    {
                        Remove("CampusGear.AuthorizedResetExpiresAt");
                    }
                    else
                    {
                        SetString("CampusGear.AuthorizedResetExpiresAt", value.ExpiresAtUnixSeconds.ToString());
                    }
                }
            }

            private AccountChallengeState? Read(string userKey, string stampKey)
            {
                return GetString(userKey) is { } id ? new(id, GetString(stampKey) ?? "") : null;
            }

            private void Write(string userKey, string stampKey, AccountChallengeState? value)
            {
                if (value is null)
                {
                    Remove(userKey);
                    Remove(stampKey);
                }
                else
                {
                    SetString(userKey, value.UserId);
                    SetString(stampKey, value.SecurityStamp);
                }
            }

            public string? GetString(string key) => Values.GetValueOrDefault(key);

            public void SetString(string key, string value) => Values[key] = value;

            public void Remove(string key) => Values.Remove(key);

            public void Clear() => Values.Clear();
        }
    }
}
