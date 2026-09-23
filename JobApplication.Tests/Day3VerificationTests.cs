using JobApplication.API.Controllers;
using JobApplication.Application.DTOs;
using JobApplication.Application.Interfaces;
using JobApplication.Application.Services;
using JobApplication.Domain.Entities;
using JobApplication.Domain.Enums;
using JobApplication.Infrastructure.Auth;
using JobApplication.Infrastructure.Persistence;
using JobApplication.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MediatR;
using JobApplication.Application.Commands.Jobs.CloseJob;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using Hangfire;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;

namespace JobApplication.Tests
{
    public class Day3VerificationTests
    {
        static Day3VerificationTests()
        {
            JobStorage.Current = new Hangfire.InMemory.InMemoryStorage();
        }
        private ApplicationDbContext CreateInMemoryDbContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;

            return new ApplicationDbContext(options);
        }

        private IConfiguration CreateTestConfiguration()
        {
            var inMemorySettings = new Dictionary<string, string?>
            {
                { "Jwt:Key", "JobApplicationSecretKeyForAuthentication2026!SecureKeyRequires256BitsMinimum" },
                { "Jwt:Issuer", "JobApplication.API" },
                { "Jwt:Audience", "JobApplication.Client" },
                { "Jwt:ExpiryMinutes", "120" }
            };

            return new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();
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

        // ==========================================
        // AUTH TESTS (Scenarios 1 - 11)
        // ==========================================

        [Fact]
        public async Task Scenarios_1_to_11_Auth_Register_Login_JWT_Validation()
        {
            var context = CreateInMemoryDbContext(nameof(Scenarios_1_to_11_Auth_Register_Login_JWT_Validation));
            var config = CreateTestConfiguration();
            var userRepo = new UserRepository(context);
            var passwordHasher = new PasswordHasher();
            var jwtGenerator = new JwtTokenGenerator(config);
            var authService = new AuthService(userRepo, passwordHasher, jwtGenerator);

            // 1. Candidate can register
            var candidateReg = await authService.RegisterAsync(new RegisterDto
            {
                Email = "candidate@example.com",
                Password = "Password123!",
                Role = UserRole.Candidate,
                FullName = "John Candidate"
            });
            Assert.True(candidateReg.Success);
            Assert.NotNull(candidateReg.Response);
            Assert.Equal("candidate@example.com", candidateReg.Response.Email);
            Assert.Equal(UserRole.Candidate, candidateReg.Response.Role);
            Assert.NotNull(candidateReg.Response.CandidateId);

            // 2. Recruiter can register
            var recruiterReg = await authService.RegisterAsync(new RegisterDto
            {
                Email = "recruiter@example.com",
                Password = "Password123!",
                Role = UserRole.Recruiter,
                FullName = "Jane Recruiter"
            });
            Assert.True(recruiterReg.Success);
            Assert.NotNull(recruiterReg.Response);
            Assert.Equal("recruiter@example.com", recruiterReg.Response.Email);
            Assert.Equal(UserRole.Recruiter, recruiterReg.Response.Role);
            Assert.NotNull(recruiterReg.Response.RecruiterId);

            // 10. Duplicate email is rejected
            var duplicateReg = await authService.RegisterAsync(new RegisterDto
            {
                Email = "candidate@example.com",
                Password = "AnotherPassword123!",
                Role = UserRole.Candidate
            });
            Assert.False(duplicateReg.Success);
            Assert.Equal("A user with this email already exists.", duplicateReg.Error);

            // 3. Candidate can login
            var candidateLogin = await authService.LoginAsync(new LoginDto
            {
                Email = "candidate@example.com",
                Password = "Password123!"
            });
            Assert.True(candidateLogin.Success);
            Assert.NotNull(candidateLogin.Response);

            // 4. Recruiter can login
            var recruiterLogin = await authService.LoginAsync(new LoginDto
            {
                Email = "recruiter@example.com",
                Password = "Password123!"
            });
            Assert.True(recruiterLogin.Success);
            Assert.NotNull(recruiterLogin.Response);

            // 11. Invalid password is rejected
            var invalidLogin = await authService.LoginAsync(new LoginDto
            {
                Email = "candidate@example.com",
                Password = "WrongPassword!"
            });
            Assert.False(invalidLogin.Success);
            Assert.Equal("Invalid email or password.", invalidLogin.Error);

            // 5. JWT is returned
            Assert.False(string.IsNullOrWhiteSpace(candidateLogin.Response.Token));
            Assert.False(string.IsNullOrWhiteSpace(recruiterLogin.Response.Token));

            // Parse candidate token
            var tokenHandler = new JwtSecurityTokenHandler();
            var candidateToken = tokenHandler.ReadJwtToken(candidateLogin.Response.Token);

            // 6. JWT contains correct userId
            var candUserIdClaim = candidateToken.Payload["userId"]?.ToString();
            Assert.Equal(candidateLogin.Response.UserId.ToString(), candUserIdClaim);

            // 7. JWT contains correct role
            var candRoleClaim = candidateToken.Payload["role"]?.ToString();
            Assert.Equal("Candidate", candRoleClaim);

            // 8. Candidate JWT contains candidateId
            var candidateIdClaim = candidateToken.Payload["candidateId"]?.ToString();
            Assert.Equal(candidateLogin.Response.CandidateId.ToString(), candidateIdClaim);

            // Parse recruiter token
            var recruiterToken = tokenHandler.ReadJwtToken(recruiterLogin.Response.Token);

            // 9. Recruiter JWT contains recruiterId
            var recruiterIdClaim = recruiterToken.Payload["recruiterId"]?.ToString();
            Assert.Equal(recruiterLogin.Response.UserId.ToString(), recruiterIdClaim);
            Assert.Equal("Recruiter", recruiterToken.Payload["role"]?.ToString());
        }

        // ==========================================
        // JOB TESTS (Scenarios 12 - 21)
        // ==========================================

        [Fact]
        public async Task Scenarios_12_to_21_Job_Lifecycle_Ownership_And_Closing()
        {
            var context = CreateInMemoryDbContext(nameof(Scenarios_12_to_21_Job_Lifecycle_Ownership_And_Closing));
            var jobRepo = new JobRepository(context);
            var jobService = new JobService(jobRepo);

            int ownerRecruiterId = 10;
            int otherRecruiterId = 20;

            // 12. Recruiter can create Job
            var createDto = new CreateJobDto
            {
                Title = "Senior .NET Engineer",
                Description = "Exciting backend role"
            };
            var jobId = await jobService.CreateAsync(createDto, ownerRecruiterId);
            Assert.True(jobId > 0);

            // 13. Created Job contains correct RecruiterId
            var job = await jobRepo.GetByIdAsync(jobId);
            Assert.NotNull(job);
            Assert.Equal(ownerRecruiterId, job.RecruiterId);
            Assert.True(job.IsActive);
            Assert.Null(job.ClosedAt);
            Assert.Null(job.ClosedBy);

            // 16. Another Recruiter cannot close the Job (Forbidden)
            var closeForbidden = await jobService.CloseAsync(jobId, otherRecruiterId);
            Assert.Equal(CloseJobResult.Forbidden, closeForbidden);

            // 15. Owner Recruiter can close Job
            var closeSuccess = await jobService.CloseAsync(jobId, ownerRecruiterId);
            Assert.Equal(CloseJobResult.Success, closeSuccess);

            // 18. Closed Job has IsActive = false
            var closedJob = await jobRepo.GetByIdAsync(jobId);
            Assert.NotNull(closedJob);
            Assert.False(closedJob.IsActive);

            // 19. Closed Job has ClosedAt populated
            Assert.NotNull(closedJob.ClosedAt);

            // 20. Closed Job has ClosedBy populated
            Assert.Equal(ownerRecruiterId, closedJob.ClosedBy);

            // 21. Closing an already closed Job returns AlreadyClosed (409 Conflict)
            var closeAgain = await jobService.CloseAsync(jobId, ownerRecruiterId);
            Assert.Equal(CloseJobResult.AlreadyClosed, closeAgain);

            // Non-existent job returns NotFound
            var closeNonExistent = await jobService.CloseAsync(9999, ownerRecruiterId);
            Assert.Equal(CloseJobResult.NotFound, closeNonExistent);
        }

        [Fact]
        public async Task JobsController_Enforces_Ownership_And_Responses()
        {
            var context = CreateInMemoryDbContext(nameof(JobsController_Enforces_Ownership_And_Responses));
            var jobRepo = new JobRepository(context);
            var jobService = new JobService(jobRepo);
            var mediator = CreateTestMediator(jobRepo);
            var controller = new JobsController(jobService, mediator);

            int recruiterId = 42;

            // Set up authenticated Recruiter ClaimsPrincipal
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, recruiterId.ToString()),
                new Claim("userId", recruiterId.ToString()),
                new Claim("recruiterId", recruiterId.ToString()),
                new Claim(ClaimTypes.Role, "Recruiter")
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            // Recruiter creates Job via API
            var createResult = await controller.Create(new CreateJobDto
            {
                Title = "Lead Architect",
                Description = "Lead systems architecture"
            });
            var okResult = Assert.IsType<OkObjectResult>(createResult);
            Assert.NotNull(okResult.Value);

            // Recruiter closes Job via API
            var createdJob = await context.Jobs.FirstAsync();
            var closeResult = await controller.Close(createdJob.Id);
            var closeOk = Assert.IsType<OkObjectResult>(closeResult);
            Assert.NotNull(closeOk.Value);

            // Attempting to close again returns 409 Conflict
            var closeConflictResult = await controller.Close(createdJob.Id);
            Assert.IsType<ConflictObjectResult>(closeConflictResult);
        }

        // ==========================================
        // APPLICATION TESTS (Scenarios 22 - 35)
        // ==========================================

        [Fact]
        public async Task Scenarios_22_to_35_Application_Lifecycle_CloseJob_And_Cancel()
        {
            var context = CreateInMemoryDbContext(nameof(Scenarios_22_to_35_Application_Lifecycle_CloseJob_And_Cancel));
            var jobRepo = new JobRepository(context);
            var appRepo = new JobApplicationRepository(context);
            var jobService = new JobService(jobRepo);
            var appService = new JobApplicationService(appRepo, jobRepo);

            int recruiterId = 1;
            int candidate1Id = 100;
            int candidate2Id = 200;

            // Create an active job
            var jobId = await jobService.CreateAsync(new CreateJobDto
            {
                Title = "Backend Developer",
                Description = "ASP.NET Core / Clean Architecture"
            }, recruiterId);

            // 22. Candidate can apply to active Job
            var apply1 = await appService.ApplyAsync(jobId, candidate1Id);
            Assert.Equal(ApplyJobResult.Success, apply1);

            // 24. Candidate cannot apply twice to the same Job
            var applyDuplicate = await appService.ApplyAsync(jobId, candidate1Id);
            Assert.Equal(ApplyJobResult.AlreadyApplied, applyDuplicate);

            // Candidate 2 also applies to the active job
            var apply2 = await appService.ApplyAsync(jobId, candidate2Id);
            Assert.Equal(ApplyJobResult.Success, apply2);

            // Now close the Job
            var closeJobResult = await jobService.CloseAsync(jobId, recruiterId);
            Assert.Equal(CloseJobResult.Success, closeJobResult);

            // 25. Existing applications remain after Job is closed
            var appCandidate1 = await context.JobCandidateApplications
                .FirstOrDefaultAsync(a => a.JobId == jobId && a.CandidateId == candidate1Id);
            var appCandidate2 = await context.JobCandidateApplications
                .FirstOrDefaultAsync(a => a.JobId == jobId && a.CandidateId == candidate2Id);

            Assert.NotNull(appCandidate1);
            Assert.NotNull(appCandidate2);

            // 26. Closing Job does NOT change existing application status
            Assert.Equal(JobApplicationStatus.Applied, appCandidate1.JobApplicationStatus);
            Assert.Equal(JobApplicationStatus.Applied, appCandidate2.JobApplicationStatus);
            Assert.Null(appCandidate1.CancelledAt);
            Assert.Null(appCandidate2.CancelledAt);

            // 23. Candidate cannot apply to closed Job
            int candidate3Id = 300;
            var applyToClosedJob = await appService.ApplyAsync(jobId, candidate3Id);
            Assert.Equal(ApplyJobResult.JobClosed, applyToClosedJob);

            // 29. Candidate cannot cancel another Candidate's application (Forbidden)
            var cancelForbidden = await appService.CancelAsync(appCandidate1.Id, candidate2Id);
            Assert.Equal(CancelApplicationResult.Forbidden, cancelForbidden);

            // 27. Candidate can cancel own Applied application
            var cancelSuccess = await appService.CancelAsync(appCandidate1.Id, candidate1Id);
            Assert.Equal(CancelApplicationResult.Success, cancelSuccess);

            // 33. Cancelled application remains in database (not deleted)
            var cancelledAppInDb = await appRepo.GetByIdAsync(appCandidate1.Id);
            Assert.NotNull(cancelledAppInDb);
            Assert.Equal(JobApplicationStatus.Cancelled, cancelledAppInDb.JobApplicationStatus);

            // 34. CancelledAt is populated
            Assert.NotNull(cancelledAppInDb.CancelledAt);

            // 35. StatusUpdatedAt is updated
            Assert.True(cancelledAppInDb.StatusUpdatedAt >= cancelledAppInDb.AppliedAt);

            // 32. Candidate cannot cancel already Cancelled application
            var cancelAgain = await appService.CancelAsync(appCandidate1.Id, candidate1Id);
            Assert.Equal(CancelApplicationResult.InvalidStatus, cancelAgain);

            // 28. Candidate can cancel own UnderReview application
            appCandidate2.JobApplicationStatus = JobApplicationStatus.UnderReview;
            appRepo.Update(appCandidate2);
            await appRepo.SaveChangesAsync();

            var cancelUnderReview = await appService.CancelAsync(appCandidate2.Id, candidate2Id);
            Assert.Equal(CancelApplicationResult.Success, cancelUnderReview);

            // 30 & 31: Cannot cancel Accepted or Rejected application
            var acceptedApp = new JobCandidateApplication
            {
                CandidateId = candidate1Id,
                JobId = jobId,
                JobApplicationStatus = JobApplicationStatus.Accepted,
                AppliedAt = DateTime.UtcNow,
                StatusUpdatedAt = DateTime.UtcNow
            };
            var rejectedApp = new JobCandidateApplication
            {
                CandidateId = candidate2Id,
                JobId = jobId,
                JobApplicationStatus = JobApplicationStatus.Rejected,
                AppliedAt = DateTime.UtcNow,
                StatusUpdatedAt = DateTime.UtcNow
            };
            context.JobCandidateApplications.Add(acceptedApp);
            context.JobCandidateApplications.Add(rejectedApp);
            await context.SaveChangesAsync();

            // 30. Candidate cannot cancel Accepted application
            var cancelAccepted = await appService.CancelAsync(acceptedApp.Id, candidate1Id);
            Assert.Equal(CancelApplicationResult.InvalidStatus, cancelAccepted);

            // 31. Candidate cannot cancel Rejected application
            var cancelRejected = await appService.CancelAsync(rejectedApp.Id, candidate2Id);
            Assert.Equal(CancelApplicationResult.InvalidStatus, cancelRejected);
        }

        [Fact]
        public async Task ApplicationsController_Enforces_Candidate_Ownership_And_Responses()
        {
            var context = CreateInMemoryDbContext(nameof(ApplicationsController_Enforces_Candidate_Ownership_And_Responses));
            var jobRepo = new JobRepository(context);
            var appRepo = new JobApplicationRepository(context);
            var jobService = new JobService(jobRepo);
            var appService = new JobApplicationService(appRepo, jobRepo);

            var controller = new ApplicationsController(appService, appRepo);

            int candidateId = 55;

            // Set up authenticated Candidate ClaimsPrincipal
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "999"),
                new Claim("userId", "999"),
                new Claim("candidateId", candidateId.ToString()),
                new Claim(ClaimTypes.Role, "Candidate")
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            // Create active job
            var jobId = await jobService.CreateAsync(new CreateJobDto
            {
                Title = "QA Automation Lead",
                Description = "Automation test lead"
            }, recruiterId: 10);

            // Apply via controller
            var applyResult = await controller.Apply(new ApplyJobDto { JobId = jobId });
            var statusCodeResult = Assert.IsType<ObjectResult>(applyResult);
            Assert.Equal(201, statusCodeResult.StatusCode);

            // Cancel via controller
            var app = await context.JobCandidateApplications.FirstAsync();
            var cancelResult = await controller.CancelApplication(app.Id);
            Assert.IsType<NoContentResult>(cancelResult);
        }

        // ==========================================
        // NEW GET ENDPOINTS TESTS
        // ==========================================

        [Fact]
        public async Task Jobs_GetEndpoints_ReturnJobs_And_JobById_Or_NotFound()
        {
            var context = CreateInMemoryDbContext(nameof(Jobs_GetEndpoints_ReturnJobs_And_JobById_Or_NotFound));
            var jobRepo = new JobRepository(context);
            var jobService = new JobService(jobRepo);
            var controller = new JobsController(jobService);

            int recruiterId = 100;

            // Initially empty
            var emptyResult = await controller.GetAll();
            var okEmpty = Assert.IsType<OkObjectResult>(emptyResult);
            var emptyList = Assert.IsAssignableFrom<List<JobResponseDto>>(okEmpty.Value);
            Assert.Empty(emptyList);

            // Create two jobs
            var job1Id = await jobService.CreateAsync(new CreateJobDto
            {
                Title = "Full Stack .NET",
                Description = "C# and React"
            }, recruiterId);

            var job2Id = await jobService.CreateAsync(new CreateJobDto
            {
                Title = "DevOps Engineer",
                Description = "Kubernetes and Azure"
            }, recruiterId);

            // Close job 1
            await jobService.CloseAsync(job1Id, recruiterId);

            // Test GET /api/Jobs
            var getAllResult = await controller.GetAll();
            var okGetAll = Assert.IsType<OkObjectResult>(getAllResult);
            var jobsList = Assert.IsAssignableFrom<List<JobResponseDto>>(okGetAll.Value);
            Assert.Equal(2, jobsList.Count);

            var job1Dto = jobsList.Find(j => j.Id == job1Id);
            Assert.NotNull(job1Dto);
            Assert.False(job1Dto.IsActive);
            Assert.NotNull(job1Dto.ClosedAt);
            Assert.Equal(recruiterId, job1Dto.ClosedBy);
            Assert.Equal(recruiterId, job1Dto.RecruiterId);

            var job2Dto = jobsList.Find(j => j.Id == job2Id);
            Assert.NotNull(job2Dto);
            Assert.True(job2Dto.IsActive);
            Assert.Null(job2Dto.ClosedAt);
            Assert.Null(job2Dto.ClosedBy);

            // Test GET /api/Jobs/{id}
            var getByIdResult = await controller.GetById(job1Id);
            var okGetById = Assert.IsType<OkObjectResult>(getByIdResult);
            var singleJob = Assert.IsType<JobResponseDto>(okGetById.Value);
            Assert.Equal(job1Id, singleJob.Id);
            Assert.False(singleJob.IsActive);
            Assert.Equal(recruiterId, singleJob.ClosedBy);

            // Test GET /api/Jobs/{id} for non-existent job -> 404
            var notFoundResult = await controller.GetById(99999);
            Assert.IsType<NotFoundObjectResult>(notFoundResult);
        }

        [Fact]
        public async Task Applications_GetEndpoints_Enforce_Role_Ownership_And_Filtering()
        {
            var context = CreateInMemoryDbContext(nameof(Applications_GetEndpoints_Enforce_Role_Ownership_And_Filtering));
            var jobRepo = new JobRepository(context);
            var appRepo = new JobApplicationRepository(context);
            var jobService = new JobService(jobRepo);
            var appService = new JobApplicationService(appRepo, jobRepo);

            int recruiter1Id = 10;
            int recruiter2Id = 20;
            int candidate1Id = 100;
            int candidate2Id = 200;

            // Recruiter 1 creates Job 1
            var job1Id = await jobService.CreateAsync(new CreateJobDto
            {
                Title = "Backend Dev",
                Description = "Job 1"
            }, recruiter1Id);

            // Recruiter 2 creates Job 2
            var job2Id = await jobService.CreateAsync(new CreateJobDto
            {
                Title = "Frontend Dev",
                Description = "Job 2"
            }, recruiter2Id);

            // Candidate 1 applies to Job 1 and Job 2
            await appService.ApplyAsync(job1Id, candidate1Id);
            await appService.ApplyAsync(job2Id, candidate1Id);

            // Candidate 2 applies to Job 1 only
            await appService.ApplyAsync(job1Id, candidate2Id);

            var appCandidate1Job1 = await context.JobCandidateApplications
                .FirstAsync(a => a.CandidateId == candidate1Id && a.JobId == job1Id);
            var appCandidate1Job2 = await context.JobCandidateApplications
                .FirstAsync(a => a.CandidateId == candidate1Id && a.JobId == job2Id);
            var appCandidate2Job1 = await context.JobCandidateApplications
                .FirstAsync(a => a.CandidateId == candidate2Id && a.JobId == job1Id);

            // 1. Test as Candidate 1
            var candidate1Controller = new ApplicationsController(appService, appRepo);
            var cand1Claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "1001"),
                new Claim("userId", "1001"),
                new Claim("candidateId", candidate1Id.ToString()),
                new Claim(ClaimTypes.Role, "Candidate"),
                new Claim("role", "Candidate")
            };
            candidate1Controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(cand1Claims, "TestAuth")) }
            };

            var cand1GetAll = await candidate1Controller.GetAll();
            var okCand1 = Assert.IsType<OkObjectResult>(cand1GetAll);
            var cand1Apps = Assert.IsAssignableFrom<List<ApplicationResponseDto>>(okCand1.Value);
            // Candidate 1 sees only their 2 applications
            Assert.Equal(2, cand1Apps.Count);
            Assert.All(cand1Apps, a => Assert.Equal(candidate1Id, a.CandidateId));

            // Candidate 1 can view their own application by ID
            var cand1GetOwn = await candidate1Controller.GetById(appCandidate1Job1.Id);
            var okOwn = Assert.IsType<OkObjectResult>(cand1GetOwn);
            var ownDto = Assert.IsType<ApplicationResponseDto>(okOwn.Value);
            Assert.Equal(appCandidate1Job1.Id, ownDto.Id);

            // Candidate 1 CANNOT view Candidate 2's application by ID -> 403 Forbidden
            var cand1GetOther = await candidate1Controller.GetById(appCandidate2Job1.Id);
            var forbiddenCand1 = Assert.IsType<ObjectResult>(cand1GetOther);
            Assert.Equal(403, forbiddenCand1.StatusCode);

            // Non-existent application -> 404 Not Found
            var cand1GetNonExistent = await candidate1Controller.GetById(99999);
            Assert.IsType<NotFoundObjectResult>(cand1GetNonExistent);

            // 2. Test as Recruiter 1
            var recruiter1Controller = new ApplicationsController(appService, appRepo);
            var rec1Claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, recruiter1Id.ToString()),
                new Claim("userId", recruiter1Id.ToString()),
                new Claim("recruiterId", recruiter1Id.ToString()),
                new Claim(ClaimTypes.Role, "Recruiter"),
                new Claim("role", "Recruiter")
            };
            recruiter1Controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(rec1Claims, "TestAuth")) }
            };

            var rec1GetAll = await recruiter1Controller.GetAll();
            var okRec1 = Assert.IsType<OkObjectResult>(rec1GetAll);
            var rec1Apps = Assert.IsAssignableFrom<List<ApplicationResponseDto>>(okRec1.Value);
            // Recruiter 1 owns Job 1, so sees applications for Job 1 only (2 applications)
            Assert.Equal(2, rec1Apps.Count);
            Assert.All(rec1Apps, a => Assert.Equal(job1Id, a.JobId));

            // Recruiter 1 can view application for Job 1 by ID
            var rec1GetJob1App = await recruiter1Controller.GetById(appCandidate1Job1.Id);
            var okRec1App = Assert.IsType<OkObjectResult>(rec1GetJob1App);
            var rec1AppDto = Assert.IsType<ApplicationResponseDto>(okRec1App.Value);
            Assert.Equal(appCandidate1Job1.Id, rec1AppDto.Id);

            // Recruiter 1 CANNOT view application for Job 2 (owned by Recruiter 2) -> 403 Forbidden
            var rec1GetJob2App = await recruiter1Controller.GetById(appCandidate1Job2.Id);
            var forbiddenRec1 = Assert.IsType<ObjectResult>(rec1GetJob2App);
            Assert.Equal(403, forbiddenRec1.StatusCode);
        }
    }
}
