using JobApplication.API.Extensions;
using JobApplication.Application.DTOs;
using JobApplication.Application.Interfaces;
using JobApplication.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace JobApplication.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class JobsController : ControllerBase
    {
        private readonly IJobService _jobService;

        public JobsController(IJobService jobService)
        {
            _jobService = jobService;
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAll()
        {
            var jobs = await _jobService.GetAllAsync();
            return Ok(jobs);
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetById(int id)
        {
            var job = await _jobService.GetByIdAsync(id);
            if (job == null)
            {
                return NotFound(new { message = "Job not found." });
            }

            return Ok(job);
        }

        [HttpPost]
        [Authorize(Roles = "Recruiter")]
        public async Task<IActionResult> Create([FromBody] CreateJobDto createJobDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var recruiterId = User.GetRecruiterId();
            if (!recruiterId.HasValue)
            {
                return Unauthorized(new { message = "Invalid or missing recruiter identity in token." });
            }

            var id = await _jobService.CreateAsync(createJobDto, recruiterId.Value);
            return Ok(new { id = id });
        }

        [HttpPut("{id}/close")]
        [Authorize(Roles = "Recruiter")]
        public async Task<IActionResult> Close(int id)
        {
            var recruiterId = User.GetRecruiterId();
            if (!recruiterId.HasValue)
            {
                return Unauthorized(new { message = "Invalid or missing recruiter identity in token." });
            }

            var result = await _jobService.CloseAsync(id, recruiterId.Value);

            return result switch
            {
                CloseJobResult.Success => Ok(new { message = "Job successfully closed." }),
                CloseJobResult.NotFound => NotFound(new { message = "Job not found." }),
                CloseJobResult.Forbidden => StatusCode(403, new { message = "You do not have permission to close this job." }),
                CloseJobResult.AlreadyClosed => Conflict(new { message = "Job is already closed." }),
                _ => BadRequest()
            };
        }
    }
}
