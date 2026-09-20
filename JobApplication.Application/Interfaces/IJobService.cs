using JobApplication.Application.DTOs;
using JobApplication.Application.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JobApplication.Application.Interfaces
{
    public interface IJobService
    {
        Task<int> CreateAsync(CreateJobDto createJobDto, int recruiterId);
        Task<CloseJobResult> CloseAsync(int jobId, int recruiterId);
        Task<List<JobResponseDto>> GetAllAsync();
        Task<JobResponseDto?> GetByIdAsync(int id);
    }
}
