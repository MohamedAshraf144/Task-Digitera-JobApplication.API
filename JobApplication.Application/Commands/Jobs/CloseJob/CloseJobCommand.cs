using JobApplication.Application.Services;
using MediatR;

namespace JobApplication.Application.Commands.Jobs.CloseJob
{
    public record CloseJobCommand(int JobId, int RecruiterId) : IRequest<CloseJobResult>;
}
