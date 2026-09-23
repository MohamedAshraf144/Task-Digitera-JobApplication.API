using JobApplication.Application.Interfaces;
using JobApplication.Application.Services;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace JobApplication.Application.Commands.Jobs.CloseJob
{
    public class CloseJobCommandHandler : IRequestHandler<CloseJobCommand, CloseJobResult>
    {
        private readonly IJobRepository _jobRepository;

        public CloseJobCommandHandler(IJobRepository jobRepository)
        {
            _jobRepository = jobRepository;
        }

        public async Task<CloseJobResult> Handle(CloseJobCommand request, CancellationToken cancellationToken)
        {
            var job = await _jobRepository.GetByIdAsync(request.JobId);
            if (job == null)
            {
                return CloseJobResult.NotFound;
            }

            if (job.RecruiterId != request.RecruiterId)
            {
                return CloseJobResult.Forbidden;
            }

            if (!job.IsActive)
            {
                return CloseJobResult.AlreadyClosed;
            }

            job.IsActive = false;
            job.ClosedAt = DateTime.UtcNow;
            job.ClosedBy = request.RecruiterId;

            _jobRepository.Update(job);
            await _jobRepository.SaveChangesAsync();

            return CloseJobResult.Success;
        }
    }
}
