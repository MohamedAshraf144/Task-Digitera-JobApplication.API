using JobApplication.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace JobApplication.Application.DTOs
{
    public class RegisterDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;

        [Required]
        public UserRole Role { get; set; }

        public string? FullName { get; set; }

        public string? CvUrl { get; set; }
    }
}
