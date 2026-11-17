using System.Text.Json;
using ASI.Basecode.Data.Models;

namespace ASI.Basecode.Services.Manager
{
    /// <summary>
    /// Turns a stored audit event (system codes such as "Auth.LoginPassword" / "SecondFactorRequired")
    /// into plain words for the System Audit Log: who did it, what happened, and how it ended.
    /// </summary>
    public static class AuditDescriptions
    {
        public sealed record Description(string Who, string? WhoEmail, string WhatHappened, string Result, string ResultState);

        private static readonly (string Text, string State) Successful = ("Successful", "approved");

        private static (string Text, string State) Failed(string text) => (text, "rejected");

        private static (string Text, string State) Waiting(string text) => (text, "pending");

        public static Description Describe(AuditEvent audit, AuditTarget? target)
        {
            // A deleted record can no longer be looked up, so fall back to the name saved with the change.
            var name = target?.Name ?? NameFromSnapshot(audit.BeforeJson) ?? NameFromSnapshot(audit.AfterJson);
            var person = target?.Person ?? "a borrower";
            var actor = audit.ActorUser?.FullName;

            // Sign-in events happen before anyone is signed in; the person is the account they are about.
            var who = actor ?? (audit.Action.StartsWith("Auth.", StringComparison.Ordinal) ? name ?? "Unknown visitor" : "CampusGear (automatic)");
            var (what, result) = Explain(audit.Action, audit.Outcome, name, person, actor);
            return new(who, audit.ActorUser?.Email, what, result.Text, result.State);
        }

        private static (string What, (string Text, string State) Result) Explain(
            string action,
            string outcome,
            string? name,
            string person,
            string? actor
        )
        {
            var ok = outcome is "Succeeded" or "Verified";
            return action switch
            {
                "Auth.Login" => ("Signed in", Successful),
                "Auth.Logout" => ("Signed out", Successful),
                "Auth.LoginPassword" => outcome switch
                {
                    "SecondFactorRequired" => ("Entered their password; a sign-in code was emailed", Waiting("Code sent")),
                    "CodeDeliveryFailed" => ("Entered their password, but the sign-in code could not be emailed", Failed("Email not sent")),
                    "PendingApproval" => ("Tried to sign in before their registration was approved", Waiting("Not yet approved")),
                    _ => ("Tried to sign in", Failed("Wrong email or password")),
                },
                "Auth.AdministratorCode" => ("Entered the administrator sign-in code", ok ? Successful : CodeProblem(outcome)),
                "Auth.SignUp" => outcome switch
                {
                    "CreatedPendingApproval" => ("Registered as a new student", Waiting("Waiting for approval")),
                    "CreatedPendingVerification" or "ResumedPendingVerification" => ("Registered as a new student", Waiting("Waiting for email check")),
                    "DuplicateEmail" => ("Tried to register", Failed("Email already registered")),
                    "DuplicateStudentId" => ("Tried to register", Failed("Student ID already registered")),
                    _ => ("Tried to register", Failed(Words(outcome))),
                },
                "Auth.PasswordResetRequest" => outcome switch
                {
                    "Sent" => ("Asked for a password reset code", Successful),
                    "Cooldown" => ("Asked for a password reset code again too soon", Waiting("Try again later")),
                    "DeliveryUnavailable" => ("Asked for a password reset code", Failed("Email not sent")),
                    _ => ("Asked for a password reset code", Failed(Words(outcome))),
                },
                "Auth.EmailVerification" => ("Confirmed their email address", ok ? Successful : CodeProblem(outcome)),
                "Auth.PasswordResetCode" => ("Entered a password reset code", ok ? Successful : CodeProblem(outcome)),
                "Auth.PasswordReset" => ok
                    ? ("Changed their password", Successful)
                    : ("Tried to change their password", Failed("Password not accepted")),

                "Admin.RegistrationApproved" => ($"Approved the registration of {name ?? "a student"}", Successful),
                "Admin.RegistrationRejected" => ($"Rejected the registration of {name ?? "a student"}", Successful),
                "Admin.UserCreated" => ($"Created an account for {name ?? "a new user"}", Successful),
                "Admin.UserUpdated" => ($"Updated the account of {name ?? "a user"}", Successful),
                "Admin.UserDeactivated" => ($"Deactivated the account of {name ?? "a user"}", Successful),
                "Admin.UserDeleted" => ($"Deleted the account of {name ?? "a user"}", Successful),

                "reservation.created" => ($"Requested to borrow {name ?? "equipment"}", Successful),
                "reservation.approved" => ($"Approved {person}'s request to borrow {name ?? "equipment"}", Successful),
                "reservation.rejected" => ($"Rejected {person}'s request to borrow {name ?? "equipment"}", Successful),
                "reservation.cancelled" => actor is not null && actor == person
                    ? ($"Cancelled their request to borrow {name ?? "equipment"}", Successful)
                    : ($"Cancelled {person}'s reservation of {name ?? "equipment"}", Successful),
                "reservation.expired" => ($"{Capitalize(person)}'s request to borrow {name ?? "equipment"} expired before it was approved", Waiting("Expired")),
                "reservation.not_picked_up" => ($"{Capitalize(person)} did not pick up {name ?? "equipment"} before the booking ended", Waiting("Not picked up")),
                "reservation.released" => ($"Released {name ?? "equipment"} to {person}", Successful),
                "reservation.returned" => ($"Recorded the return of {name ?? "equipment"} from {person}", Successful),

                "category.created" => ($"Added the equipment category {name ?? ""}".TrimEnd(), Successful),
                "category.updated" => ($"Updated the equipment category {name ?? ""}".TrimEnd(), Successful),
                "category.deactivated" => ($"Deactivated the equipment category {name ?? ""}".TrimEnd(), Successful),
                "category.deleted" => ($"Deleted the equipment category {name ?? ""}".TrimEnd(), Successful),

                "equipment.created" => ($"Added the equipment {name ?? ""}".TrimEnd(), Successful),
                "equipment.updated" => ($"Updated the equipment {name ?? ""}".TrimEnd(), Successful),
                "equipment.deactivated" => ($"Deactivated the equipment {name ?? ""}".TrimEnd(), Successful),
                "equipment.deleted" => ($"Deleted the equipment {name ?? ""}".TrimEnd(), Successful),

                "maintenance.opened" => ($"Put {name ?? "equipment"} under maintenance", Successful),
                "maintenance.updated" => ($"Updated the maintenance of {name ?? "equipment"}", Successful),

                _ => (Words(action), ok ? Successful : Failed(Words(outcome))),
            };
        }

        private static (string Text, string State) CodeProblem(string outcome)
        {
            return outcome switch
            {
                "Expired" => Failed("Code expired"),
                "TooManyAttempts" => Failed("Too many wrong tries"),
                "StaleFlow" => Failed("Request expired"),
                "AccountUnavailable" => Failed("Account not available"),
                _ => Failed("Wrong code"),
            };
        }

        /// <summary>"Admin.UserUpdated" / "SecondFactorRequired" → "Admin user updated" / "Second factor required".</summary>
        private static string Words(string code)
        {
            var spaced = System.Text.RegularExpressions.Regex.Replace(code.Replace('.', ' ').Replace('_', ' '), "(?<=[a-z])(?=[A-Z])", " ");
            return Capitalize(spaced.ToLowerInvariant());
        }

        private static string Capitalize(string text)
        {
            return text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
        }

        private static string? NameFromSnapshot(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                string? Read(string property)
                {
                    foreach (var field in document.RootElement.EnumerateObject())
                    {
                        if (field.Name.Equals(property, StringComparison.OrdinalIgnoreCase) && field.Value.ValueKind == JsonValueKind.String)
                        {
                            return field.Value.GetString();
                        }
                    }

                    return null;
                }

                var full = Read("FullName") ?? $"{Read("FirstName")} {Read("LastName")}".Trim();
                return string.IsNullOrWhiteSpace(full) ? Read("Name") : full;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
