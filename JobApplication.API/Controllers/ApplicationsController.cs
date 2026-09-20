using JobApplication.API.Extensions;
using JobApplication.Application.DTOs;
using JobApplication.Application.Interfaces;
using JobApplication.Application.Services;
using JobApplication.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace JobApplication.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ApplicationsController : ControllerBase
    {
        private readonly IJobApplicationService _jobApplicationService;
        private readonly IJobApplicationRepository _jobApplicationRepository;

        public ApplicationsController(
            IJobApplicationService jobApplicationService,
            IJobApplicationRepository jobApplicationRepository)
        {
            _jobApplicationService = jobApplicationService;
            _jobApplicationRepository = jobApplicationRepository;
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAll()
        {
            var role = User.GetUserRole();
            if (role == UserRole.Candidate)
            {
                var candidateId = await ResolveCandidateIdAsync();
                if (!candidateId.HasValue)
                {
                    return Unauthorized(new { message = "Invalid or missing candidate identity in token." });
                }

                var applications = await _jobApplicationService.GetApplicationsForCandidateAsync(candidateId.Value);
                return Ok(applications);
            }
            else if (role == UserRole.Recruiter)
            {
                var recruiterId = User.GetRecruiterId();
                if (!recruiterId.HasValue)
                {
                    return Unauthorized(new { message = "Invalid or missing recruiter identity in token." });
                }

                var applications = await _jobApplicationService.GetApplicationsForRecruiterAsync(recruiterId.Value);
                return Ok(applications);
            }

            return StatusCode(403, new { message = "User role not authorized to view applications." });
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetById(int id)
        {
            var role = User.GetUserRole();
            if (!role.HasValue)
            {
                return Unauthorized(new { message = "Missing or invalid role in token." });
            }

            int actorId;
            if (role.Value == UserRole.Candidate)
            {
                var candidateId = await ResolveCandidateIdAsync();
                if (!candidateId.HasValue)
                {
                    return Unauthorized(new { message = "Invalid or missing candidate identity in token." });
                }
                actorId = candidateId.Value;
            }
            else if (role.Value == UserRole.Recruiter)
            {
                var recruiterId = User.GetRecruiterId();
                if (!recruiterId.HasValue)
                {
                    return Unauthorized(new { message = "Invalid or missing recruiter identity in token." });
                }
                actorId = recruiterId.Value;
            }
            else
            {
                return StatusCode(403, new { message = "User role not authorized to view this application." });
            }

            var (result, application) = await _jobApplicationService.GetByIdAsync(id, role.Value, actorId);

            return result switch
            {
                GetApplicationResult.Success => Ok(application),
                GetApplicationResult.NotFound => NotFound(new { message = "Application not found." }),
                GetApplicationResult.Forbidden => StatusCode(403, new { message = "You do not have permission to view this application." }),
                _ => BadRequest()
            };
        }

        [HttpPost]
        [Authorize(Roles = "Candidate")]
        public async Task<IActionResult> Apply([FromBody] ApplyJobDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var candidateId = await ResolveCandidateIdAsync();
            if (!candidateId.HasValue)
            {
                return Unauthorized(new { message = "Invalid or missing candidate identity in token." });
            }

            var result = await _jobApplicationService.ApplyAsync(dto.JobId, candidateId.Value);

            return result switch
            {
                ApplyJobResult.Success => StatusCode(201, new { message = "Application submitted successfully." }),
                ApplyJobResult.NotFound => NotFound(new { message = "Job not found." }),
                ApplyJobResult.JobClosed => Conflict(new { message = "Cannot apply to a closed job." }),
                ApplyJobResult.AlreadyApplied => Conflict(new { message = "You have already applied to this job." }),
                _ => BadRequest()
            };
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Candidate")]
        public async Task<IActionResult> CancelApplication(int id)
        {
            var candidateId = await ResolveCandidateIdAsync();
            if (!candidateId.HasValue)
            {
                return Unauthorized(new { message = "Invalid or missing candidate identity in token." });
            }

            var result = await _jobApplicationService.CancelAsync(id, candidateId.Value);

            return result switch
            {
                CancelApplicationResult.Success => NoContent(),
                CancelApplicationResult.NotFound => NotFound(new { message = "Application not found." }),
                CancelApplicationResult.Forbidden => StatusCode(403, new { message = "You do not have permission to cancel this application." }),
                CancelApplicationResult.InvalidStatus => Conflict(new { message = "Application cannot be cancelled in its current status." }),
                _ => BadRequest()
            };
        }

        private async Task<int?> ResolveCandidateIdAsync()
        {
            var candidateId = User.GetCandidateId();
            if (candidateId.HasValue)
            {
                return candidateId.Value;
            }

            var userId = User.GetUserId();
            if (userId.HasValue)
            {
                var candidate = await _jobApplicationRepository.GetCandidateByUserIdAsync(userId.Value);
                if (candidate != null)
                {
                    return candidate.Id;
                }
            }

            return null;
        }
    }
}
