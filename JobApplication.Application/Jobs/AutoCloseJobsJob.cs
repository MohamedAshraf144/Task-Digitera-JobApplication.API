using JobApplication.Application.Configuration;
using JobApplication.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace JobApplication.Application.Jobs
{
    public class AutoCloseJobsJob : IAutoCloseJobsJob
    {
        private readonly IJobRepository _jobRepository;
        private readonly IOptions<JobSettings> _jobSettings;
        private readonly ILogger<AutoCloseJobsJob> _logger;

        public AutoCloseJobsJob(
            IJobRepository jobRepository,
            IOptions<JobSettings> jobSettings,
            ILogger<AutoCloseJobsJob> logger)
        {
            _jobRepository = jobRepository;
            _jobSettings = jobSettings;
            _logger = logger;
        }

        public async Task ExecuteAsync()
        {
            var thresholdDays = _jobSettings.Value.AutoCloseAfterDays;
            var cutoffDate = DateTime.UtcNow.AddDays(-thresholdDays);

            _logger.LogInformation("Running auto-close jobs recurring job. Finding active jobs created before {CutoffDate} (older than {ThresholdDays} days)...",
                cutoffDate, thresholdDays);

            var oldJobs = await _jobRepository.GetActiveJobsOlderThanAsync(cutoffDate);

            if (oldJobs.Count == 0)
            {
                _logger.LogInformation("No active jobs found that exceed the {ThresholdDays} days threshold.", thresholdDays);
                return;
            }

            foreach (var job in oldJobs)
            {
                if (!job.IsActive)
                {
                    continue;
                }

                // Consistent with Close Job rules:
                // - IsActive becomes false
                // - ClosedAt is populated
                // - ClosedBy is null for system-automated closure (no human recruiter performed the action)
                job.IsActive = false;
                job.ClosedAt = DateTime.UtcNow;
                job.ClosedBy = null;

                _jobRepository.Update(job);
                _logger.LogInformation("Auto-closed job ID {JobId} ('{Title}'). CreatedAt: {CreatedAt}",
                    job.Id, job.Title, job.CreatedAt);
            }

            await _jobRepository.SaveChangesAsync();
            _logger.LogInformation("Auto-closed {Count} job(s) successfully.", oldJobs.Count);
        }
    }
}
