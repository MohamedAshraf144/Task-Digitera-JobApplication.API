using JobApplication.API.Controllers;
using JobApplication.Application.Commands.Jobs.CloseJob;
using JobApplication.Application.DTOs;
using JobApplication.Application.Interfaces;
using JobApplication.Application.Services;
using JobApplication.Domain.Entities;
using JobApplication.Domain.Enums;
using JobApplication.Infrastructure.Persistence;
using JobApplication.Infrastructure.Repositories;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace JobApplication.Tests
{
    public class Day4CqrsVerificationTests
    {
        private ApplicationDbContext CreateInMemoryDbContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;

            return new ApplicationDbContext(options);
        }

        private IMediator CreateTestMediator(IJobRepository jobRepository)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(jobRepository);
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CloseJobCommand).Assembly));
            var provider = services.BuildServiceProvider();
            return provider.GetRequiredService<IMediator>();
        }

        private static ControllerContext CreateControllerContext(int userId, string role, int? recruiterId = null)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim("userId", userId.ToString()),
                new Claim(ClaimTypes.Role, role)
            };

            if (recruiterId.HasValue)
            {
                claims.Add(new Claim("recruiterId", recruiterId.Value.ToString()));
            }

            var identity = new ClaimsIdentity(claims, "TestAuth");
            return new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };
        }

        // ============================================================
        // 1. Owner recruiter can close a Job
        // 6. ClosedAt is populated
        // 7. ClosedBy contains the correct recruiter ID
        // 8. IsActive becomes false
        // ============================================================
        [Fact]
        public async Task Requirement_1_6_7_8_OwnerRecruiter_CanCloseJob_Successfully()
        {
            var context = CreateInMemoryDbContext(nameof(Requirement_1_6_7_8_OwnerRecruiter_CanCloseJob_Successfully));
            var jobRepo = new JobRepository(context);
            var mediator = CreateTestMediator(jobRepo);

            int ownerRecruiterId = 10;
            var job = new Job
            {
                Title = "Senior Backend Engineer",
                Description = "C# and .NET 10",
                IsActive = true,
                RecruiterId = ownerRecruiterId
            };
            await jobRepo.InsertAsync(job);
            await jobRepo.SaveChangesAsync();

            var beforeUtc = DateTime.UtcNow.AddSeconds(-1);
            var result = await mediator.Send(new CloseJobCommand(job.Id, ownerRecruiterId));
            var afterUtc = DateTime.UtcNow.AddSeconds(1);

            Assert.Equal(CloseJobResult.Success, result);

            var updatedJob = await jobRepo.GetByIdAsync(job.Id);
            Assert.NotNull(updatedJob);
            Assert.False(updatedJob.IsActive); // 8. IsActive becomes false
            Assert.NotNull(updatedJob.ClosedAt); // 6. ClosedAt is populated
            Assert.InRange(updatedJob.ClosedAt.Value, beforeUtc, afterUtc);
            Assert.Equal(ownerRecruiterId, updatedJob.ClosedBy); // 7. ClosedBy contains the correct recruiter ID
        }

        // ============================================================
        // 2. Another recruiter cannot close the Job (Forbidden)
        // ============================================================
        [Fact]
        public async Task Requirement_2_AnotherRecruiter_CannotCloseJob_ReturnsForbidden()
        {
            var context = CreateInMemoryDbContext(nameof(Requirement_2_AnotherRecruiter_CannotCloseJob_ReturnsForbidden));
            var jobRepo = new JobRepository(context);
            var mediator = CreateTestMediator(jobRepo);

            int ownerRecruiterId = 10;
            int anotherRecruiterId = 99;

            var job = new Job
            {
                Title = "Cloud Architect",
                Description = "Azure architecture",
                IsActive = true,
                RecruiterId = ownerRecruiterId
            };
            await jobRepo.InsertAsync(job);
            await jobRepo.SaveChangesAsync();

            var result = await mediator.Send(new CloseJobCommand(job.Id, anotherRecruiterId));

            Assert.Equal(CloseJobResult.Forbidden, result);

            // Verify job state was NOT mutated
            var unchangedJob = await jobRepo.GetByIdAsync(job.Id);
            Assert.NotNull(unchangedJob);
            Assert.True(unchangedJob.IsActive);
            Assert.Null(unchangedJob.ClosedAt);
            Assert.Null(unchangedJob.ClosedBy);
        }

        // ============================================================
        // 3. Candidate cannot close the Job
        // ============================================================
        [Fact]
        public void Requirement_3_Candidate_CannotCloseJob_EnforcedByAuthorizeAttribute()
        {
            // Verify endpoint requires Role = Recruiter
            var method = typeof(JobsController).GetMethod(nameof(JobsController.Close));
            Assert.NotNull(method);

            var authorizeAttr = method.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(authorizeAttr);
            Assert.Equal("Recruiter", authorizeAttr.Roles);
        }

        [Fact]
        public async Task Requirement_3_Candidate_AttemptingToCloseJob_ReturnsForbidden()
        {
            var context = CreateInMemoryDbContext(nameof(Requirement_3_Candidate_AttemptingToCloseJob_ReturnsForbidden));
            var jobRepo = new JobRepository(context);
            var jobService = new JobService(jobRepo);
            var mediator = CreateTestMediator(jobRepo);
            var controller = new JobsController(jobService, mediator);

            int ownerRecruiterId = 10;
            var job = new Job
            {
                Title = "Frontend Developer",
                Description = "Vue.js",
                IsActive = true,
                RecruiterId = ownerRecruiterId
            };
            await jobRepo.InsertAsync(job);
            await jobRepo.SaveChangesAsync();

            // Candidate (userId 55) attempts to close the job
            controller.ControllerContext = CreateControllerContext(55, "Candidate");

            var response = await controller.Close(job.Id);
            var statusResult = Assert.IsType<ObjectResult>(response);
            Assert.Equal(StatusCodes.Status403Forbidden, statusResult.StatusCode);
        }

        [Fact]
        public async Task Requirement_MissingRecruiterIdentity_ReturnsUnauthorized()
        {
            var context = CreateInMemoryDbContext(nameof(Requirement_MissingRecruiterIdentity_ReturnsUnauthorized));
            var jobRepo = new JobRepository(context);
            var jobService = new JobService(jobRepo);
            var mediator = CreateTestMediator(jobRepo);
            var controller = new JobsController(jobService, mediator);

            // User without userId / recruiterId / nameIdentifier claims
            var identity = new ClaimsIdentity();
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            var response = await controller.Close(1);
            Assert.IsType<UnauthorizedObjectResult>(response);
        }

        // ============================================================
        // 4. Non-existent Job returns NotFound
        // ============================================================
        [Fact]
        public async Task Requirement_4_NonExistentJob_ReturnsNotFound()
        {
            var context = CreateInMemoryDbContext(nameof(Requirement_4_NonExistentJob_ReturnsNotFound));
            var jobRepo = new JobRepository(context);
            var mediator = CreateTestMediator(jobRepo);

            var result = await mediator.Send(new CloseJobCommand(99999, 10));

            Assert.Equal(CloseJobResult.NotFound, result);
        }

        // ============================================================
        // 5. Already closed Job returns Conflict
        // ============================================================
        [Fact]
        public async Task Requirement_5_AlreadyClosedJob_ReturnsAlreadyClosed()
        {
            var context = CreateInMemoryDbContext(nameof(Requirement_5_AlreadyClosedJob_ReturnsAlreadyClosed));
            var jobRepo = new JobRepository(context);
            var mediator = CreateTestMediator(jobRepo);

            int ownerRecruiterId = 10;
            var job = new Job
            {
                Title = "QA Lead",
                Description = "Testing automation",
                IsActive = true,
                RecruiterId = ownerRecruiterId
            };
            await jobRepo.InsertAsync(job);
            await jobRepo.SaveChangesAsync();

            // First close -> Success
            var firstResult = await mediator.Send(new CloseJobCommand(job.Id, ownerRecruiterId));
            Assert.Equal(CloseJobResult.Success, firstResult);

            // Second close -> AlreadyClosed
            var secondResult = await mediator.Send(new CloseJobCommand(job.Id, ownerRecruiterId));
            Assert.Equal(CloseJobResult.AlreadyClosed, secondResult);
        }

        // ============================================================
        // 9. Existing applications are NOT cancelled when Job is closed
        // ============================================================
        [Fact]
        public async Task Requirement_9_ExistingApplications_AreNotCancelled_WhenJobIsClosed()
        {
            var context = CreateInMemoryDbContext(nameof(Requirement_9_ExistingApplications_AreNotCancelled_WhenJobIsClosed));
            var jobRepo = new JobRepository(context);
            var appRepo = new JobApplicationRepository(context);
            var mediator = CreateTestMediator(jobRepo);

            int ownerRecruiterId = 10;
            var job = new Job
            {
                Title = "DevOps Specialist",
                Description = "CI/CD and Kubernetes",
                IsActive = true,
                RecruiterId = ownerRecruiterId
            };
            await jobRepo.InsertAsync(job);
            await jobRepo.SaveChangesAsync();

            // Add applications
            var app1 = new JobCandidateApplication
            {
                JobId = job.Id,
                CandidateId = 101,
                JobApplicationStatus = JobApplicationStatus.Applied,
                AppliedAt = DateTime.UtcNow
            };
            var app2 = new JobCandidateApplication
            {
                JobId = job.Id,
                CandidateId = 102,
                JobApplicationStatus = JobApplicationStatus.Applied,
                AppliedAt = DateTime.UtcNow
            };
            await appRepo.InsertAsync(app1);
            await appRepo.InsertAsync(app2);
            await appRepo.SaveChangesAsync();

            // Close the job via MediatR
            var closeResult = await mediator.Send(new CloseJobCommand(job.Id, ownerRecruiterId));
            Assert.Equal(CloseJobResult.Success, closeResult);

            // Verify applications are still intact and NOT cancelled
            var applications = await context.JobCandidateApplications.Where(a => a.JobId == job.Id).ToListAsync();
            Assert.Equal(2, applications.Count);
            Assert.All(applications, a => Assert.Equal(JobApplicationStatus.Applied, a.JobApplicationStatus));
        }

        // ============================================================
        // 10. The controller uses MediatR instead of directly calling JobService.CloseAsync
        // ============================================================
        private class SpyMediator : IMediator
        {
            public CloseJobCommand? CapturedCommand { get; private set; }
            public int SendCallCount { get; private set; }

            public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
            {
                if (request is CloseJobCommand command)
                {
                    CapturedCommand = command;
                    SendCallCount++;
                    return Task.FromResult((TResponse)(object)CloseJobResult.Success);
                }

                throw new NotSupportedException();
            }

            public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
            {
                throw new NotImplementedException();
            }

            public Task<object?> Send(object request, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task Publish(object notification, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
            {
                throw new NotImplementedException();
            }

            public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }
        }

        private class FailingJobService : IJobService
        {
            public Task<CloseJobResult> CloseAsync(int jobId, int recruiterId)
            {
                throw new InvalidOperationException("JobService.CloseAsync must NOT be called by the controller!");
            }

            public Task<int> CreateAsync(CreateJobDto createJobDto, int recruiterId) => throw new NotImplementedException();
            public Task<List<JobResponseDto>> GetAllAsync() => throw new NotImplementedException();
            public Task<JobResponseDto?> GetByIdAsync(int id) => throw new NotImplementedException();
        }

        [Fact]
        public async Task Requirement_10_Controller_UsesMediatR_InsteadOfJobService_CloseAsync()
        {
            var spyMediator = new SpyMediator();
            var failingJobService = new FailingJobService();
            var controller = new JobsController(failingJobService, spyMediator);

            int recruiterId = 77;
            int jobId = 505;

            controller.ControllerContext = CreateControllerContext(recruiterId, "Recruiter", recruiterId);

            var actionResult = await controller.Close(jobId);

            // If JobService.CloseAsync was called, an exception would have been thrown.
            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            Assert.NotNull(okResult.Value);

            // Verify MediatR was called exactly once with the correct parameters
            Assert.Equal(1, spyMediator.SendCallCount);
            Assert.NotNull(spyMediator.CapturedCommand);
            Assert.Equal(jobId, spyMediator.CapturedCommand.JobId);
            Assert.Equal(recruiterId, spyMediator.CapturedCommand.RecruiterId);
        }

        [Fact]
        public async Task Controller_Maps_All_MediatR_CloseJobResults_Correctly()
        {
            var jobService = new FailingJobService();

            async Task AssertResponse<TExpectedResult>(CloseJobResult handlerResult) where TExpectedResult : ObjectResult
            {
                var mockMediator = new CustomResultMediator(handlerResult);
                var controller = new JobsController(jobService, mockMediator);
                controller.ControllerContext = CreateControllerContext(1, "Recruiter", 1);

                var response = await controller.Close(10);
                Assert.IsType<TExpectedResult>(response);
            }

            await AssertResponse<OkObjectResult>(CloseJobResult.Success);
            await AssertResponse<NotFoundObjectResult>(CloseJobResult.NotFound);
            await AssertResponse<ObjectResult>(CloseJobResult.Forbidden);
            await AssertResponse<ConflictObjectResult>(CloseJobResult.AlreadyClosed);
        }

        private class CustomResultMediator : IMediator
        {
            private readonly CloseJobResult _result;

            public CustomResultMediator(CloseJobResult result)
            {
                _result = result;
            }

            public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
            {
                return Task.FromResult((TResponse)(object)_result);
            }

            public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotImplementedException();
            public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task Publish(object notification, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => throw new NotImplementedException();
            public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        }
    }
}
