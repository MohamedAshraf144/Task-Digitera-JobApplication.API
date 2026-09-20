using JobApplication.Domain.Entities;
using System.Threading.Tasks;

namespace JobApplication.Application.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(int id);
        Task<User?> GetByEmailAsync(string email);
        Task<Candidate?> GetCandidateByUserIdAsync(int userId);
        Task InsertUserAsync(User user);
        Task InsertCandidateAsync(Candidate candidate);
        Task SaveChangesAsync();
    }
}
