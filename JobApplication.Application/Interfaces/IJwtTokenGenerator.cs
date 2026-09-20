using JobApplication.Domain.Entities;

namespace JobApplication.Application.Interfaces
{
    public interface IJwtTokenGenerator
    {
        string GenerateToken(User user, int? candidateId);
    }
}
