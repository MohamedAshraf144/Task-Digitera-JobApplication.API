using JobApplication.Application.DTOs;
using System.Threading.Tasks;

namespace JobApplication.Application.Interfaces
{
    public interface IAuthService
    {
        Task<(bool Success, string? Error, AuthResponseDto? Response)> RegisterAsync(RegisterDto dto);
        Task<(bool Success, string? Error, AuthResponseDto? Response)> LoginAsync(LoginDto dto);
    }
}
