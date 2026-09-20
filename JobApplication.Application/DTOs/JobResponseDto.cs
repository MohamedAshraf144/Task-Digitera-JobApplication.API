using System;

namespace JobApplication.Application.DTOs
{
    public class JobResponseDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int RecruiterId { get; set; }
        public DateTime? ClosedAt { get; set; }
        public int? ClosedBy { get; set; }
    }
}
