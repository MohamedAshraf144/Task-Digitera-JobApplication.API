using JobApplication.Application.Interfaces;
using JobApplication.Domain.Entities;
using JobApplication.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace JobApplication.Infrastructure.Repositories
{
    public class JobApplicationRepository : IJobApplicationRepository
    {
        private readonly ApplicationDbContext _context;

        public JobApplicationRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<JobCandidateApplication?> GetByIdAsync(int id)
        {
            return await _context.JobCandidateApplications.FindAsync(id);
        }

        public async Task<JobCandidateApplication?> GetByIdWithJobAsync(int id)
        {
            return await _context.JobCandidateApplications
                .Include(a => a.Job)
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task<List<JobCandidateApplication>> GetByCandidateIdAsync(int candidateId)
        {
            return await _context.JobCandidateApplications
                .Where(a => a.CandidateId == candidateId)
                .ToListAsync();
        }

        public async Task<List<JobCandidateApplication>> GetByRecruiterIdAsync(int recruiterId)
        {
            return await _context.JobCandidateApplications
                .Include(a => a.Job)
                .Where(a => a.Job != null && a.Job.RecruiterId == recruiterId)
                .ToListAsync();
        }

        public async Task<bool> ExistsAsync(int candidateId, int jobId)
        {
            return await _context.JobCandidateApplications
                .AnyAsync(a => a.CandidateId == candidateId && a.JobId == jobId);
        }

        public async Task InsertAsync(JobCandidateApplication jobApplication)
        {
            await _context.JobCandidateApplications.AddAsync(jobApplication);
        }

        public async Task<Candidate?> GetCandidateByUserIdAsync(int userId)
        {
            return await _context.Candidates.FirstOrDefaultAsync(c => c.UserId == userId);
        }

        public async Task<Candidate?> GetCandidateByIdAsync(int id)
        {
            return await _context.Candidates.FindAsync(id);
        }

        public void Update(JobCandidateApplication jobApplication)
        {
            _context.JobCandidateApplications.Update(jobApplication);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
