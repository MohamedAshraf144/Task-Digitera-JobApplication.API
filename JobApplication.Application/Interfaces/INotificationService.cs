using System.Threading.Tasks;

namespace JobApplication.Application.Interfaces
{
    public interface INotificationService
    {
        Task NotifyCandidate(int applicationId);
    }
}
