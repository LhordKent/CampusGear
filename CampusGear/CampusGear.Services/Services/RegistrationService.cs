using CampusGear.Data.Interfaces;
using CampusGear.Data.Models;
using CampusGear.Resources.Constants;
using CampusGear.Resources.Messages;
using CampusGear.Services.Helpers;
using CampusGear.Services.Interfaces;
using CampusGear.Services.ServiceModels;
using Microsoft.AspNetCore.Identity;

namespace CampusGear.Services.Services
{
    public sealed class RegistrationService : IRegistrationService
    {
        private readonly IAccountRepository _accountRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _audit;

        public RegistrationService(
            IAccountRepository accounts,
            IUnitOfWork work,
            UserManager<ApplicationUser> userManager,
            IAuditService audit
        )
        {
            _accountRepository = accounts;
            _unitOfWork = work;
            _userManager = userManager;
            _audit = audit;
        }

        public async Task<OperationResult<RegistrationResult>> SignUpAsync(
            RequestContext actor,
            SignUpInput input,
            CancellationToken ct = default
        )
        {
            var feedback = new AccountFeedback();
            var email = input.Email.Trim();
            var studentId = input.StudentIdNumber.Trim();
            if (await _userManager.FindByEmailAsync(email) is not null)
            {
                await _audit.RecordAccountAsync(actor, "Auth.SignUp", "DuplicateEmail", null, ct);
                feedback.AddError(nameof(input.Email), RegistrationMessages.EmailAlreadyRegistered);
            }

            if (await _accountRepository.StudentExistsAsync(studentId, null, ct))
            {
                await _audit.RecordAccountAsync(actor, "Auth.SignUp", "DuplicateStudentId", null, ct);
                feedback.AddError(nameof(input.StudentIdNumber), RegistrationMessages.StudentNumberAlreadyRegistered);
            }

            if (feedback.Errors.Count > 0)
            {
                return feedback.Result(new RegistrationResult(RegistrationStatus.InputRequired));
            }

            ApplicationUser user;
            try
            {
                await using var transaction = await _unitOfWork.BeginAsync(ct);
                user = new ApplicationUser
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
                    feedback.Error = RegistrationMessages.AccountCreationUnavailable;
                    return feedback.Result(new RegistrationResult(RegistrationStatus.InputRequired));
                }

                _accountRepository.AddProfile(
                    new BorrowerProfile
                    {
                        UserId = user.Id,
                        StudentNumber = studentId,
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
                    feedback.AddError(nameof(input.StudentIdNumber), RegistrationMessages.StudentNumberAlreadyRegistered);
                }
                else
                {
                    feedback.AddError(nameof(input.Email), RegistrationMessages.EmailAlreadyRegistered);
                }

                return feedback.Result(new RegistrationResult(RegistrationStatus.InputRequired));
            }

            await _audit.RecordAccountAsync(actor, "Auth.SignUp", "CreatedPendingApproval", user.Id, ct);
            return feedback.Result(new RegistrationResult(RegistrationStatus.Submitted));
        }
    }
}
