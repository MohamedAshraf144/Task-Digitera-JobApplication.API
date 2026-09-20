using JobApplication.Domain.Enums;
using System;
using System.Security.Claims;

namespace JobApplication.API.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        public static int? GetUserId(this ClaimsPrincipal user)
        {
            var idClaim = user.FindFirst("userId")?.Value
                ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (int.TryParse(idClaim, out var id) && id > 0)
            {
                return id;
            }

            return null;
        }

        public static int? GetCandidateId(this ClaimsPrincipal user)
        {
            var candidateClaim = user.FindFirst("candidateId")?.Value;
            if (int.TryParse(candidateClaim, out var candidateId) && candidateId > 0)
            {
                return candidateId;
            }

            return null;
        }

        public static int? GetRecruiterId(this ClaimsPrincipal user)
        {
            var recruiterClaim = user.FindFirst("recruiterId")?.Value
                ?? user.FindFirst("userId")?.Value
                ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (int.TryParse(recruiterClaim, out var recruiterId) && recruiterId > 0)
            {
                return recruiterId;
            }

            return null;
        }

        public static UserRole? GetUserRole(this ClaimsPrincipal user)
        {
            var roleClaim = user.FindFirst(ClaimTypes.Role)?.Value
                ?? user.FindFirst("role")?.Value;

            if (Enum.TryParse<UserRole>(roleClaim, true, out var role))
            {
                return role;
            }

            return null;
        }
    }
}
