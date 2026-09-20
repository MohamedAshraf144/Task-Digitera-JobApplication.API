using System.ComponentModel.DataAnnotations;

namespace JobApplication.Application.DTOs
{
    public class ApplyJobDto
    {
        [Required]
        public int JobId { get; set; }
    }
}
