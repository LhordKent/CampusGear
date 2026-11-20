using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Resources.Messages;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.AspNetCore.Identity;

// =====================================================================
// REGISTRATION SERVICE  -  student self sign-up (/auth/signup).
// Creates the account as "Pending approval" with a borrower profile. The student can't sign in
// until an administrator approves it on the Users & Roles page (UserService.DecideAsync).
// =====================================================================
namespace ASI.Basecode.Services.Services
{
    public sealed class RegistrationService : ServiceBase, IRegistrationService
    {
        private readonly IUserRepository _accountRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<User> _userManager;
        private readonly IAuditService _audit;

        public RegistrationService(
            IUserRepository accounts,
            IUnitOfWork work,
            UserManager<User> userManager,
            IAuditService audit
        )
        {
            _accountRepository = accounts;
            _unitOfWork = work;
            _userManager = userManager;
            _audit = audit;
        }

        // Validates the form, then creates the user, the Borrower role and the borrower profile together.
        public async Task<OperationResult<RegistrationResult>> SignUpAsync(
            RequestContext actor,
            SignUpInput input,
            CancellationToken ct = default
        )
        {
            var feedback = new AccountFeedback();
            var email = input.Email.Trim();
            var studentId = input.StudentIdNumber.Trim();
            // Public sign-up only accepts a listed department, saved with the list's spelling.
            var department = Departments.Find(input.Department);
            if (department is null)
            {
                feedback.AddError(nameof(input.Department), Errors.DepartmentNotListed);
            }

            if (await _userManager.FindByEmailAsync(email) is not null)
            {
                await _audit.RecordAccountAsync(actor, "Auth.SignUp", "DuplicateEmail", null, ct);
                feedback.AddError(nameof(input.Email), Errors.EmailAlreadyRegistered);
            }

            if (await _accountRepository.StudentExistsAsync(studentId, null, ct))
            {
                await _audit.RecordAccountAsync(actor, "Auth.SignUp", "DuplicateStudentId", null, ct);
                feedback.AddError(nameof(input.StudentIdNumber), Errors.StudentNumberAlreadyRegistered);
            }

            if (feedback.Errors.Count > 0)
            {
                return feedback.Result(new RegistrationResult(RegistrationStatus.InputRequired));
            }

            User user;
            try
            {
                await using var transaction = await _unitOfWork.BeginAsync(ct);
                user = new User
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = false,
                    FirstName = input.FirstName.Trim(),
                    LastName = input.LastName.Trim(),
                    PhoneNumber = input.MobileNumber.Trim(),
                    ApprovalStatus = AccountApprovalStatus.PendingApproval,
                    IsActive = true,
                };
                var created = await _userManager.CreateAsync(user, input.Password);
                if (!created.Succeeded)
                {
                    foreach (var error in created.Errors)
                    {
                        feedback.AddError(nameof(input.Password), error.Description);
                    }

                    return feedback.Result(new RegistrationResult(RegistrationStatus.InputRequired));
                }

                var assigned = await _userManager.AddToRoleAsync(user, RoleNames.Borrower);
                if (!assigned.Succeeded)
                {
                    feedback.Error = Errors.AccountCreationUnavailable;
                    return feedback.Result(new RegistrationResult(RegistrationStatus.InputRequired));
                }

                _accountRepository.AddProfile(
                    new BorrowerProfile
                    {
                        UserId = user.Id,
                        StudentNumber = studentId,
                        Department = department,
                        ContactNumber = input.MobileNumber.Trim(),
                        IsEligible = true,
                    }
                );
                await _unitOfWork.SaveAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (PersistenceException exception) when (exception.Failure == PersistenceFailure.Duplicate)
            {
                // The unique indexes also protect a race between simultaneous signups.
                _unitOfWork.ClearTracking();
                if (exception.Constraint == "StudentNumber")
                {
                    feedback.AddError(nameof(input.StudentIdNumber), Errors.StudentNumberAlreadyRegistered);
                }
                else
                {
                    feedback.AddError(nameof(input.Email), Errors.EmailAlreadyRegistered);
                }

                return feedback.Result(new RegistrationResult(RegistrationStatus.InputRequired));
            }

            await _audit.RecordAccountAsync(actor, "Auth.SignUp", "CreatedPendingApproval", user.Id, ct);
            return feedback.Result(new RegistrationResult(RegistrationStatus.Submitted));
        }
    }
}
