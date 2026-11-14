using CampusGear.Services.ServiceModels;

namespace CampusGear.Services.Helpers
{
    internal sealed class AccountFeedback
    {
        public string? Error { get; set; }

        public string? Notice { get; set; }

        public List<FieldError> Errors { get; } = [];

        public void AddError(string field, string message)
        {
            Errors.Add(new(field, message));
        }

        public OperationResult<T> Result<T>(T value)
        {
            return new(Error is null && Errors.Count == 0 ? OperationOutcome.Success : OperationOutcome.Invalid, value, Error, Errors);
        }
    }
}
