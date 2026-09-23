using System.Threading.Tasks;

namespace JobApplication.Application.Interfaces
{
    public interface IAutoCloseJobsJob
    {
        Task ExecuteAsync();
    }
}
