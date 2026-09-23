using JobApplication.Application.DTOs;
using JobApplication.Application.Interfaces;
using JobApplication.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace JobApplication.Application.Services
{
    public class JobService : IJobService
    {
        private readonly IJobRepository _jobRepository;

        public JobService(IJobRepository jobRepository)
        {
            _jobRepository = jobRepository;
        }

        public async Task<int> CreateAsync(CreateJobDto createJobDto, int recruiterId)
        {
            var job = new Job
            {
                Title = createJobDto.Title,
                Description = createJobDto.Description,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                RecruiterId = recruiterId
            };

            await _jobRepository.InsertAsync(job);
            await _jobRepository.SaveChangesAsync();

            return job.Id;
        }

        public async Task<CloseJobResult> CloseAsync(int jobId, int recruiterId)
        {
            var job = await _jobRepository.GetByIdAsync(jobId);
            if (job == null)
            {
                return CloseJobResult.NotFound;
            }

            if (job.RecruiterId != recruiterId)
            {
                return CloseJobResult.Forbidden;
            }

            if (!job.IsActive)
            {
                return CloseJobResult.AlreadyClosed;
            }

            job.IsActive = false;
            job.ClosedAt = DateTime.UtcNow;
            job.ClosedBy = recruiterId;

            _jobRepository.Update(job);
            await _jobRepository.SaveChangesAsync();

            return CloseJobResult.Success;
        }

        public async Task<List<JobResponseDto>> GetAllAsync()
        {
            var jobs = await _jobRepository.GetAllAsync();
            return jobs.Select(j => new JobResponseDto
            {
                Id = j.Id,
                Title = j.Title,
                Description = j.Description,
                IsActive = j.IsActive,
                RecruiterId = j.RecruiterId,
                ClosedAt = j.ClosedAt,
                ClosedBy = j.ClosedBy
            }).ToList();
        }

        public async Task<JobResponseDto?> GetByIdAsync(int id)
        {
            var job = await _jobRepository.GetByIdAsync(id);
            if (job == null)
            {
                return null;
            }

            return new JobResponseDto
            {
                Id = job.Id,
                Title = job.Title,
                Description = job.Description,
                IsActive = job.IsActive,
                RecruiterId = job.RecruiterId,
                ClosedAt = job.ClosedAt,
                ClosedBy = job.ClosedBy
            };
        }
    }
}
