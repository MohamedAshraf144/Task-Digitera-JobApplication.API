using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace JobApplication.Domain.Entities
{
    public class Job
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int RecruiterId { get; set; }
        [ForeignKey(nameof(RecruiterId))]
        public User? Recruiter { get; set; }
        public DateTime? ClosedAt { get; set; }
        public int? ClosedBy { get; set; }
        [ForeignKey(nameof(ClosedBy))]
        public User? ClosedByUser { get; set; }
    }
}
