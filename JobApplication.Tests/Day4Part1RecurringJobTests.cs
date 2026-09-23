using JobApplication.Application.Configuration;
using JobApplication.Application.Jobs;
using JobApplication.Domain.Entities;
using JobApplication.Domain.Enums;
using JobApplication.Infrastructure.Persistence;
using JobApplication.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;
using Xunit;

namespace JobApplication.Tests
{
    public class Day4Part1RecurringJobTests
    {
        private ApplicationDbContext CreateInMemoryDbContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;

            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task ActiveOldJob_ExceedingThreshold_IsClosed_AndClosedAtPopulated()
        {
            var context = CreateInMemoryDbContext(nameof(ActiveOldJob_ExceedingThreshold_IsClosed_AndClosedAtPopulated));
            var jobRepo = new JobRepository(context);
            var settings = Options.Create(new JobSettings { AutoCloseAfterDays = 30 });
            var jobService = new AutoCloseJobsJob(jobRepo, settings, NullLogger<AutoCloseJobsJob>.Instance);

            var oldJob = new Job
            {
                Title = "Old Senior Engineer",
                Description = "Opened long ago",
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-35),
                RecruiterId = 1
            };
            await jobRepo.InsertAsync(oldJob);
            await jobRepo.SaveChangesAsync();

            // Act
            await jobService.ExecuteAsync();

            // Assert
            var updated = await jobRepo.GetByIdAsync(oldJob.Id);
            Assert.NotNull(updated);
            Assert.False(updated.IsActive);
            Assert.NotNull(updated.ClosedAt);
            Assert.Null(updated.ClosedBy); // System automation closure has no human recruiter
        }

        [Fact]
        public async Task ActiveRecentJob_BelowThreshold_RemainsOpen()
        {
            var context = CreateInMemoryDbContext(nameof(ActiveRecentJob_BelowThreshold_RemainsOpen));
            var jobRepo = new JobRepository(context);
            var settings = Options.Create(new JobSettings { AutoCloseAfterDays = 30 });
            var jobService = new AutoCloseJobsJob(jobRepo, settings, NullLogger<AutoCloseJobsJob>.Instance);

            var recentJob = new Job
            {
                Title = "Recent Junior Developer",
                Description = "Just opened",
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                RecruiterId = 1
            };
            await jobRepo.InsertAsync(recentJob);
            await jobRepo.SaveChangesAsync();

            // Act
            await jobService.ExecuteAsync();

            // Assert
            var updated = await jobRepo.GetByIdAsync(recentJob.Id);
            Assert.NotNull(updated);
            Assert.True(updated.IsActive);
            Assert.Null(updated.ClosedAt);
            Assert.Null(updated.ClosedBy);
        }

        [Fact]
        public async Task AlreadyClosedJob_IsNotModified()
        {
            var context = CreateInMemoryDbContext(nameof(AlreadyClosedJob_IsNotModified));
            var jobRepo = new JobRepository(context);
            var settings = Options.Create(new JobSettings { AutoCloseAfterDays = 30 });
            var jobService = new AutoCloseJobsJob(jobRepo, settings, NullLogger<AutoCloseJobsJob>.Instance);

            var originalClosedAt = DateTime.UtcNow.AddDays(-10);
            int closedByRecruiterId = 42;

            var alreadyClosedJob = new Job
            {
                Title = "Manually Closed Job",
                Description = "Closed earlier by recruiter",
                IsActive = false,
                CreatedAt = DateTime.UtcNow.AddDays(-60),
                ClosedAt = originalClosedAt,
                ClosedBy = closedByRecruiterId,
                RecruiterId = 1
            };
            await jobRepo.InsertAsync(alreadyClosedJob);
            await jobRepo.SaveChangesAsync();

            // Act
            await jobService.ExecuteAsync();

            // Assert
            var updated = await jobRepo.GetByIdAsync(alreadyClosedJob.Id);
            Assert.NotNull(updated);
            Assert.False(updated.IsActive);
            Assert.Equal(originalClosedAt, updated.ClosedAt);
            Assert.Equal(closedByRecruiterId, updated.ClosedBy);
        }

        [Fact]
        public async Task ExistingApplications_ForAutoClosedJob_RemainIntact()
        {
            var context = CreateInMemoryDbContext(nameof(ExistingApplications_ForAutoClosedJob_RemainIntact));
            var jobRepo = new JobRepository(context);
            var appRepo = new JobApplicationRepository(context);
            var settings = Options.Create(new JobSettings { AutoCloseAfterDays = 30 });
            var jobService = new AutoCloseJobsJob(jobRepo, settings, NullLogger<AutoCloseJobsJob>.Instance);

            var oldJob = new Job
            {
                Title = "Lead Architect",
                Description = "Applications exist",
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-40),
                RecruiterId = 2
            };
            await jobRepo.InsertAsync(oldJob);
            await jobRepo.SaveChangesAsync();

            var app1 = new JobCandidateApplication
            {
                JobId = oldJob.Id,
                CandidateId = 101,
                JobApplicationStatus = JobApplicationStatus.Applied,
                AppliedAt = DateTime.UtcNow.AddDays(-20),
                StatusUpdatedAt = DateTime.UtcNow.AddDays(-20)
            };
            var app2 = new JobCandidateApplication
            {
                JobId = oldJob.Id,
                CandidateId = 102,
                JobApplicationStatus = JobApplicationStatus.UnderReview,
                AppliedAt = DateTime.UtcNow.AddDays(-15),
                StatusUpdatedAt = DateTime.UtcNow.AddDays(-10)
            };
            await appRepo.InsertAsync(app1);
            await appRepo.InsertAsync(app2);
            await appRepo.SaveChangesAsync();

            // Act
            await jobService.ExecuteAsync();

            // Assert: Job is closed
            var updatedJob = await jobRepo.GetByIdAsync(oldJob.Id);
            Assert.NotNull(updatedJob);
            Assert.False(updatedJob.IsActive);
            Assert.NotNull(updatedJob.ClosedAt);

            // Assert: Applications remain intact with original statuses
            var updatedApp1 = await appRepo.GetByIdAsync(app1.Id);
            Assert.NotNull(updatedApp1);
            Assert.Equal(JobApplicationStatus.Applied, updatedApp1.JobApplicationStatus);
            Assert.Null(updatedApp1.CancelledAt);

            var updatedApp2 = await appRepo.GetByIdAsync(app2.Id);
            Assert.NotNull(updatedApp2);
            Assert.Equal(JobApplicationStatus.UnderReview, updatedApp2.JobApplicationStatus);
            Assert.Null(updatedApp2.CancelledAt);
        }

        [Fact]
        public async Task CustomThreshold_IsRespected()
        {
            var context = CreateInMemoryDbContext(nameof(CustomThreshold_IsRespected));
            var jobRepo = new JobRepository(context);
            // Custom threshold of 10 days instead of default 30
            var settings = Options.Create(new JobSettings { AutoCloseAfterDays = 10 });
            var jobService = new AutoCloseJobsJob(jobRepo, settings, NullLogger<AutoCloseJobsJob>.Instance);

            var jobOlderThan10Days = new Job
            {
                Title = "12 Day Old Job",
                Description = "Should be closed under 10-day rule",
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-12),
                RecruiterId = 5
            };
            var jobYoungerThan10Days = new Job
            {
                Title = "8 Day Old Job",
                Description = "Should stay open under 10-day rule",
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-8),
                RecruiterId = 5
            };

            await jobRepo.InsertAsync(jobOlderThan10Days);
            await jobRepo.InsertAsync(jobYoungerThan10Days);
            await jobRepo.SaveChangesAsync();

            // Act
            await jobService.ExecuteAsync();

            // Assert
            var updatedOld = await jobRepo.GetByIdAsync(jobOlderThan10Days.Id);
            Assert.NotNull(updatedOld);
            Assert.False(updatedOld.IsActive);
            Assert.NotNull(updatedOld.ClosedAt);

            var updatedYoung = await jobRepo.GetByIdAsync(jobYoungerThan10Days.Id);
            Assert.NotNull(updatedYoung);
            Assert.True(updatedYoung.IsActive);
            Assert.Null(updatedYoung.ClosedAt);
        }

        [Fact]
        public async Task RecurringJob_ExecutesWithoutError_WhenNoActiveJobsExist()
        {
            var context = CreateInMemoryDbContext(nameof(RecurringJob_ExecutesWithoutError_WhenNoActiveJobsExist));
            var jobRepo = new JobRepository(context);
            var settings = Options.Create(new JobSettings { AutoCloseAfterDays = 30 });
            var jobService = new AutoCloseJobsJob(jobRepo, settings, NullLogger<AutoCloseJobsJob>.Instance);

            // Act & Assert (should complete gracefully with zero errors)
            await jobService.ExecuteAsync();
        }
    }
}
