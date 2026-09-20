using JobApplication.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JobApplication.Application.Interfaces
{
    public interface IJobApplicationRepository
    {
        Task<JobCandidateApplication?> GetByIdAsync(int id);
        Task<JobCandidateApplication?> GetByIdWithJobAsync(int id);
        Task<List<JobCandidateApplication>> GetByCandidateIdAsync(int candidateId);
        Task<List<JobCandidateApplication>> GetByRecruiterIdAsync(int recruiterId);
        Task<bool> ExistsAsync(int candidateId, int jobId);
        Task InsertAsync(JobCandidateApplication jobApplication);
        Task<Candidate?> GetCandidateByUserIdAsync(int userId);
        Task<Candidate?> GetCandidateByIdAsync(int id);
        void Update(JobCandidateApplication jobApplication);
        Task SaveChangesAsync();
    }
}
