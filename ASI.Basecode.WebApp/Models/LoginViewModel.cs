using System.ComponentModel.DataAnnotations;

namespace ASI.Basecode.WebApp.Models
{
    /// <summary>WBS 4.00 | The Email and Password field names in Login.cshtml bind to these properties.</summary>
    public class LoginViewModel
    {
        [Required, EmailAddress, StringLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }
}
