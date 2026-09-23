using Hangfire;
using Hangfire.InMemory;
using JobApplication.Application.DTOs;
using JobApplication.Application.Interfaces;
using JobApplication.Application.Services;
using JobApplication.Domain.Entities;
using JobApplication.Domain.Enums;
using JobApplication.Infrastructure.Notifications;
using JobApplication.Infrastructure.Persistence;
using JobApplication.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace JobApplication.Tests
{
    public class Day4HangfireVerificationTests
    {
        private InMemoryStorage _inMemoryStorage;

        public Day4HangfireVerificationTests()
        {
            _inMemoryStorage = new InMemoryStorage();
            JobStorage.Current = _inMemoryStorage;
        }

        private ApplicationDbContext CreateInMemoryDbContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;

            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task SuccessfulCancellation_FromApplied_ChangesStatusToCancelled_AndEnqueuesNotification()
        {
            var context = CreateInMemoryDbContext(nameof(SuccessfulCancellation_FromApplied_ChangesStatusToCancelled_AndEnqueuesNotification));
            var appRepo = new JobApplicationRepository(context);
            var jobRepo = new JobRepository(context);
            var service = new JobApplicationService(appRepo, jobRepo);

            int candidateId = 10;
            int jobId = 1;

            var application = new JobCandidateApplication
            {
                CandidateId = candidateId,
                JobId = jobId,
                JobApplicationStatus = JobApplicationStatus.Applied,
                AppliedAt = DateTime.UtcNow.AddHours(-2),
                StatusUpdatedAt = DateTime.UtcNow.AddHours(-2)
            };
            await appRepo.InsertAsync(application);
            await appRepo.SaveChangesAsync();

            var beforeCount = _inMemoryStorage.GetMonitoringApi().EnqueuedCount("default");

            // Act: Cancel application
            var result = await service.CancelAsync(application.Id, candidateId);

            // Assert business rules
            Assert.Equal(CancelApplicationResult.Success, result);

            var updated = await appRepo.GetByIdAsync(application.Id);
            Assert.NotNull(updated);
            Assert.Equal(JobApplicationStatus.Cancelled, updated.JobApplicationStatus);
            Assert.NotNull(updated.CancelledAt);
            Assert.True(updated.StatusUpdatedAt >= updated.AppliedAt);

            // Assert Hangfire background job enqueued
            var monitoringApi = _inMemoryStorage.GetMonitoringApi();
            var afterCount = monitoringApi.EnqueuedCount("default");
            Assert.Equal(beforeCount + 1, afterCount);

            var enqueuedJobs = monitoringApi.EnqueuedJobs("default", 0, 10);
            var enqueuedJob = enqueuedJobs.FirstOrDefault(j =>
                j.Value.Job.Type == typeof(INotificationService) &&
                j.Value.Job.Method.Name == nameof(INotificationService.NotifyCandidate) &&
                (int)j.Value.Job.Args[0] == application.Id);

            Assert.NotNull(enqueuedJob.Value);
            Assert.Equal(typeof(INotificationService), enqueuedJob.Value.Job.Type);
            Assert.Equal(nameof(INotificationService.NotifyCandidate), enqueuedJob.Value.Job.Method.Name);
            Assert.Equal(application.Id, (int)enqueuedJob.Value.Job.Args[0]);
        }

        [Fact]
        public async Task SuccessfulCancellation_FromUnderReview_ChangesStatusToCancelled_AndEnqueuesNotification()
        {
            var context = CreateInMemoryDbContext(nameof(SuccessfulCancellation_FromUnderReview_ChangesStatusToCancelled_AndEnqueuesNotification));
            var appRepo = new JobApplicationRepository(context);
            var jobRepo = new JobRepository(context);
            var service = new JobApplicationService(appRepo, jobRepo);

            int candidateId = 20;
            int jobId = 2;

            var application = new JobCandidateApplication
            {
                CandidateId = candidateId,
                JobId = jobId,
                JobApplicationStatus = JobApplicationStatus.UnderReview,
                AppliedAt = DateTime.UtcNow.AddHours(-3),
                StatusUpdatedAt = DateTime.UtcNow.AddHours(-1)
            };
            await appRepo.InsertAsync(application);
            await appRepo.SaveChangesAsync();

            var beforeCount = _inMemoryStorage.GetMonitoringApi().EnqueuedCount("default");

            // Act
            var result = await service.CancelAsync(application.Id, candidateId);

            // Assert
            Assert.Equal(CancelApplicationResult.Success, result);
            var updated = await appRepo.GetByIdAsync(application.Id);
            Assert.NotNull(updated);
            Assert.Equal(JobApplicationStatus.Cancelled, updated.JobApplicationStatus);
            Assert.NotNull(updated.CancelledAt);

            var afterCount = _inMemoryStorage.GetMonitoringApi().EnqueuedCount("default");
            Assert.Equal(beforeCount + 1, afterCount);

            var enqueuedJobs = _inMemoryStorage.GetMonitoringApi().EnqueuedJobs("default", 0, 10);
            var job = enqueuedJobs.FirstOrDefault(j => (int)j.Value.Job.Args[0] == application.Id);
            Assert.NotNull(job.Value);
        }

        [Fact]
        public async Task UnauthorizedCancellation_Forbidden_DoesNotChangeStatus_AndDoesNotEnqueueNotification()
        {
            var context = CreateInMemoryDbContext(nameof(UnauthorizedCancellation_Forbidden_DoesNotChangeStatus_AndDoesNotEnqueueNotification));
            var appRepo = new JobApplicationRepository(context);
            var jobRepo = new JobRepository(context);
            var service = new JobApplicationService(appRepo, jobRepo);

            int ownerCandidateId = 30;
            int unauthorizedCandidateId = 99;
            int jobId = 3;

            var application = new JobCandidateApplication
            {
                CandidateId = ownerCandidateId,
                JobId = jobId,
                JobApplicationStatus = JobApplicationStatus.Applied,
                AppliedAt = DateTime.UtcNow.AddHours(-1),
                StatusUpdatedAt = DateTime.UtcNow.AddHours(-1)
            };
            await appRepo.InsertAsync(application);
            await appRepo.SaveChangesAsync();

            var beforeCount = _inMemoryStorage.GetMonitoringApi().EnqueuedCount("default");

            // Act: Unauthorized attempt
            var result = await service.CancelAsync(application.Id, unauthorizedCandidateId);

            // Assert
            Assert.Equal(CancelApplicationResult.Forbidden, result);

            var untouched = await appRepo.GetByIdAsync(application.Id);
            Assert.NotNull(untouched);
            Assert.Equal(JobApplicationStatus.Applied, untouched.JobApplicationStatus);
            Assert.Null(untouched.CancelledAt);

            // Assert no job enqueued
            var afterCount = _inMemoryStorage.GetMonitoringApi().EnqueuedCount("default");
            Assert.Equal(beforeCount, afterCount);
        }

        [Theory]
        [InlineData(JobApplicationStatus.Accepted)]
        [InlineData(JobApplicationStatus.Rejected)]
        [InlineData(JobApplicationStatus.Cancelled)]
        public async Task InvalidStatusCancellation_DoesNotChangeStatus_AndDoesNotEnqueueNotification(JobApplicationStatus status)
        {
            var context = CreateInMemoryDbContext($"{nameof(InvalidStatusCancellation_DoesNotChangeStatus_AndDoesNotEnqueueNotification)}_{status}");
            var appRepo = new JobApplicationRepository(context);
            var jobRepo = new JobRepository(context);
            var service = new JobApplicationService(appRepo, jobRepo);

            int candidateId = 40;
            int jobId = 4;

            var application = new JobCandidateApplication
            {
                CandidateId = candidateId,
                JobId = jobId,
                JobApplicationStatus = status,
                AppliedAt = DateTime.UtcNow.AddHours(-5),
                StatusUpdatedAt = DateTime.UtcNow.AddHours(-2)
            };
            await appRepo.InsertAsync(application);
            await appRepo.SaveChangesAsync();

            var beforeCount = _inMemoryStorage.GetMonitoringApi().EnqueuedCount("default");

            // Act
            var result = await service.CancelAsync(application.Id, candidateId);

            // Assert
            Assert.Equal(CancelApplicationResult.InvalidStatus, result);

            var untouched = await appRepo.GetByIdAsync(application.Id);
            Assert.NotNull(untouched);
            Assert.Equal(status, untouched.JobApplicationStatus);

            // Assert no job enqueued
            var afterCount = _inMemoryStorage.GetMonitoringApi().EnqueuedCount("default");
            Assert.Equal(beforeCount, afterCount);
        }

        [Fact]
        public async Task NonExistentApplication_ReturnsNotFound_AndDoesNotEnqueueNotification()
        {
            var context = CreateInMemoryDbContext(nameof(NonExistentApplication_ReturnsNotFound_AndDoesNotEnqueueNotification));
            var appRepo = new JobApplicationRepository(context);
            var jobRepo = new JobRepository(context);
            var service = new JobApplicationService(appRepo, jobRepo);

            var beforeCount = _inMemoryStorage.GetMonitoringApi().EnqueuedCount("default");

            // Act
            var result = await service.CancelAsync(99999, candidateId: 1);

            // Assert
            Assert.Equal(CancelApplicationResult.NotFound, result);

            var afterCount = _inMemoryStorage.GetMonitoringApi().EnqueuedCount("default");
            Assert.Equal(beforeCount, afterCount);
        }

        [Fact]
        public async Task EmailNotificationService_Executes_WithoutError()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddScoped<INotificationService, EmailNotificationService>();
            var provider = services.BuildServiceProvider();

            var notificationService = provider.GetRequiredService<INotificationService>();
            Assert.NotNull(notificationService);
            Assert.IsType<EmailNotificationService>(notificationService);

            // Calling NotifyCandidate should complete without throwing
            await notificationService.NotifyCandidate(12345);
        }
    }
}
