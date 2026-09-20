using JobApplication.Application.DTOs;
using JobApplication.Application.Services;
using JobApplication.Domain.Enums;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JobApplication.Application.Interfaces
{
    public interface IJobApplicationService
    {
        Task<ApplyJobResult> ApplyAsync(int jobId, int candidateId);
        Task<CancelApplicationResult> CancelAsync(int applicationId, int candidateId);
        Task<List<ApplicationResponseDto>> GetApplicationsForCandidateAsync(int candidateId);
        Task<List<ApplicationResponseDto>> GetApplicationsForRecruiterAsync(int recruiterId);
        Task<(GetApplicationResult Result, ApplicationResponseDto? Application)> GetByIdAsync(int id, UserRole role, int actorId);
    }
}
