using JobApplication.Application.DTOs;
using JobApplication.Application.Interfaces;
using JobApplication.Domain.Entities;
using JobApplication.Domain.Enums;
using Hangfire;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace JobApplication.Application.Services
{
    public class JobApplicationService : IJobApplicationService
    {
        private readonly IJobApplicationRepository _repository;
        private readonly IJobRepository _jobRepository;

        public JobApplicationService(
            IJobApplicationRepository repository,
            IJobRepository jobRepository)
        {
            _repository = repository;
            _jobRepository = jobRepository;
        }

        public async Task<ApplyJobResult> ApplyAsync(int jobId, int candidateId)
        {
            var job = await _jobRepository.GetByIdAsync(jobId);
            if (job == null)
            {
                return ApplyJobResult.NotFound;
            }

            if (!job.IsActive)
            {
                return ApplyJobResult.JobClosed;
            }

            var alreadyApplied = await _repository.ExistsAsync(candidateId, jobId);
            if (alreadyApplied)
            {
                return ApplyJobResult.AlreadyApplied;
            }

            var application = new JobCandidateApplication
            {
                CandidateId = candidateId,
                JobId = jobId,
                JobApplicationStatus = JobApplicationStatus.Applied,
                AppliedAt = DateTime.UtcNow,
                StatusUpdatedAt = DateTime.UtcNow
            };

            await _repository.InsertAsync(application);
            await _repository.SaveChangesAsync();

            return ApplyJobResult.Success;
        }

        public async Task<CancelApplicationResult> CancelAsync(int applicationId, int candidateId)
        {
            var application = await _repository.GetByIdAsync(applicationId);
            if (application == null)
            {
                return CancelApplicationResult.NotFound;
            }

            if (application.CandidateId != candidateId)
            {
                return CancelApplicationResult.Forbidden;
            }

            if (application.JobApplicationStatus != JobApplicationStatus.Applied &&
                application.JobApplicationStatus != JobApplicationStatus.UnderReview)
            {
                return CancelApplicationResult.InvalidStatus;
            }

            application.JobApplicationStatus = JobApplicationStatus.Cancelled;
            application.CancelledAt = DateTime.UtcNow;
            application.StatusUpdatedAt = DateTime.UtcNow;

            _repository.Update(application);
            await _repository.SaveChangesAsync();

            BackgroundJob.Enqueue<INotificationService>(
                x => x.NotifyCandidate(applicationId));

            return CancelApplicationResult.Success;
        }

        public async Task<List<ApplicationResponseDto>> GetApplicationsForCandidateAsync(int candidateId)
        {
            var applications = await _repository.GetByCandidateIdAsync(candidateId);
            return applications.Select(MapToDto).ToList();
        }

        public async Task<List<ApplicationResponseDto>> GetApplicationsForRecruiterAsync(int recruiterId)
        {
            var applications = await _repository.GetByRecruiterIdAsync(recruiterId);
            return applications.Select(MapToDto).ToList();
        }

        public async Task<(GetApplicationResult Result, ApplicationResponseDto? Application)> GetByIdAsync(int id, UserRole role, int actorId)
        {
            var application = await _repository.GetByIdWithJobAsync(id);
            if (application == null)
            {
                return (GetApplicationResult.NotFound, null);
            }

            if (role == UserRole.Candidate)
            {
                if (application.CandidateId != actorId)
                {
                    return (GetApplicationResult.Forbidden, null);
                }
            }
            else if (role == UserRole.Recruiter)
            {
                if (application.Job == null || application.Job.RecruiterId != actorId)
                {
                    return (GetApplicationResult.Forbidden, null);
                }
            }

            return (GetApplicationResult.Success, MapToDto(application));
        }

        private static ApplicationResponseDto MapToDto(JobCandidateApplication app)
        {
            return new ApplicationResponseDto
            {
                Id = app.Id,
                JobId = app.JobId,
                CandidateId = app.CandidateId,
                JobApplicationStatus = app.JobApplicationStatus,
                AppliedAt = app.AppliedAt,
                StatusUpdatedAt = app.StatusUpdatedAt,
                CancelledAt = app.CancelledAt
            };
        }
    }
}
