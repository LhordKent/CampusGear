namespace CampusGear.Services.ServiceModels;
public enum AccountStep { Login, SignUp, RegistrationSubmitted, TwoFactor, PasswordResetRequest, PasswordResetCode, PasswordResetNew, Success, Authenticated }
public sealed record AccountFlow(AccountStep Step, bool Transition = false, string? Path = null, string? Notice = null);
public sealed record LoginCommand(string Email, string Password);
public sealed record SignUpCommand(string FirstName, string LastName, string StudentIdNumber, string Email, string MobileNumber, string Password);
public sealed record CurrentAccountDisplay(string Name, string Initial);
