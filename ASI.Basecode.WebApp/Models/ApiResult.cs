using System.Text.Json.Serialization;

namespace ASI.Basecode.WebApp.Models
{
    /// <summary>
    /// JSON answer to a modal form save (read by wwwroot/js/modal-forms.js):
    /// { ok, message, field } where field names the first invalid input on an error.
    /// </summary>
    public class ApiResult
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("field")]
        public string? Field { get; set; }

        public static ApiResult CreateSuccess(string? message)
        {
            return new ApiResult { Ok = true, Message = message };
        }

        public static ApiResult CreateError(string message, string? field)
        {
            return new ApiResult { Ok = false, Message = message, Field = field };
        }
    }
}
