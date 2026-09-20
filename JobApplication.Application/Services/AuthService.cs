using JobApplication.Application.DTOs;
using JobApplication.Application.Interfaces;
using JobApplication.Domain.Entities;
using JobApplication.Domain.Enums;
using System;
using System.Threading.Tasks;

namespace JobApplication.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IJwtTokenGenerator _jwtTokenGenerator;

        public AuthService(
            IUserRepository userRepository,
            IPasswordHasher passwordHasher,
            IJwtTokenGenerator jwtTokenGenerator)
        {
            _userRepository = userRepository;
            _passwordHasher = passwordHasher;
            _jwtTokenGenerator = jwtTokenGenerator;
        }

        public async Task<(bool Success, string? Error, AuthResponseDto? Response)> RegisterAsync(RegisterDto dto)
        {
            var normalizedEmail = dto.Email.Trim().ToLowerInvariant();

            var existingUser = await _userRepository.GetByEmailAsync(normalizedEmail);
            if (existingUser != null)
            {
                return (false, "A user with this email already exists.", null);
            }

            var passwordHash = _passwordHasher.HashPassword(dto.Password);

            var user = new User
            {
                Email = normalizedEmail,
                PasswordHash = passwordHash,
                Role = dto.Role,
                FullName = dto.FullName,
                CreatedAt = DateTime.UtcNow
            };

            await _userRepository.InsertUserAsync(user);
            await _userRepository.SaveChangesAsync();

            int? candidateId = null;
            if (dto.Role == UserRole.Candidate)
            {
                var candidate = new Candidate
                {
                    UserId = user.Id,
                    Name = !string.IsNullOrWhiteSpace(dto.FullName) ? dto.FullName : dto.Email,
                    CvUrl = dto.CvUrl ?? string.Empty
                };

                await _userRepository.InsertCandidateAsync(candidate);
                await _userRepository.SaveChangesAsync();
                candidateId = candidate.Id;
            }

            var token = _jwtTokenGenerator.GenerateToken(user, candidateId);

            var response = new AuthResponseDto
            {
                Token = token,
                UserId = user.Id,
                Email = user.Email,
                Role = user.Role,
                CandidateId = candidateId,
                RecruiterId = user.Role == UserRole.Recruiter ? user.Id : null
            };

            return (true, null, response);
        }

        public async Task<(bool Success, string? Error, AuthResponseDto? Response)> LoginAsync(LoginDto dto)
        {
            var normalizedEmail = dto.Email.Trim().ToLowerInvariant();

            var user = await _userRepository.GetByEmailAsync(normalizedEmail);
            if (user == null)
            {
                return (false, "Invalid email or password.", null);
            }

            if (!_passwordHasher.VerifyPassword(dto.Password, user.PasswordHash))
            {
                return (false, "Invalid email or password.", null);
            }

            int? candidateId = null;
            if (user.Role == UserRole.Candidate)
            {
                var candidate = await _userRepository.GetCandidateByUserIdAsync(user.Id);
                if (candidate != null)
                {
                    candidateId = candidate.Id;
                }
            }

            var token = _jwtTokenGenerator.GenerateToken(user, candidateId);

            var response = new AuthResponseDto
            {
                Token = token,
                UserId = user.Id,
                Email = user.Email,
                Role = user.Role,
                CandidateId = candidateId,
                RecruiterId = user.Role == UserRole.Recruiter ? user.Id : null
            };

            return (true, null, response);
        }
    }
}
