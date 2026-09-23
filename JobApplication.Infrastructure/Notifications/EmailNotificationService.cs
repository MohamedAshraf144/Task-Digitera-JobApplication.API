using JobApplication.Application.Interfaces;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace JobApplication.Infrastructure.Notifications
{
    public class EmailNotificationService : INotificationService
    {
        private readonly ILogger<EmailNotificationService> _logger;

        public EmailNotificationService(ILogger<EmailNotificationService> logger)
        {
            _logger = logger;
        }

        public Task NotifyCandidate(int applicationId)
        {
            _logger.LogInformation("Candidate notified for cancelled application ID: {ApplicationId}", applicationId);
            return Task.CompletedTask;
        }
    }
}
