using JobApplication.Domain.Enums;

namespace JobApplication.Application.DTOs
{
    public class AuthResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public int? CandidateId { get; set; }
        public int? RecruiterId { get; set; }
    }
}
