using JobApplication.API.Extensions;
using JobApplication.Application.Commands.Jobs.CloseJob;
using JobApplication.Application.DTOs;
using JobApplication.Application.Interfaces;
using JobApplication.Application.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace JobApplication.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class JobsController : ControllerBase
    {
        private readonly IJobService _jobService;
        private readonly IMediator _mediator;

        [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
        public JobsController(IJobService jobService, IMediator mediator)
        {
            _jobService = jobService;
            _mediator = mediator;
        }

        public JobsController(IJobService jobService)
            : this(jobService, null!)
        {
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

        /// <summary>
        /// Closes an active job.
        /// </summary>
        /// <remarks>
        /// Requires Bearer JWT authentication with the Recruiter role.
        /// The authenticated recruiter must be the owner who created the job.
        /// </remarks>
        /// <param name="id">The ID of the job to close.</param>
        /// <response code="200">Job successfully closed.</response>
        /// <response code="401">Unauthorized - Missing or invalid recruiter token.</response>
        /// <response code="403">Forbidden - Authenticated user is not a recruiter or does not own this job.</response>
        /// <response code="404">Not Found - Job with the specified ID was not found.</response>
        /// <response code="409">Conflict - Job is already closed.</response>
        [HttpPut("{id}/close")]
        [Authorize(Roles = "Recruiter")]
        [EndpointSummary("Close a job")]
        [EndpointDescription("Closes an active job. Requires Bearer JWT authentication as the Recruiter who owns the job. Only the job owner can close the job.")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Close(int id)
        {
            var recruiterId = User.GetRecruiterId();
            if (!recruiterId.HasValue)
            {
                return Unauthorized(new { message = "Invalid or missing recruiter identity in token." });
            }

            var command = new CloseJobCommand(id, recruiterId.Value);
            var result = await _mediator.Send(command);

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
