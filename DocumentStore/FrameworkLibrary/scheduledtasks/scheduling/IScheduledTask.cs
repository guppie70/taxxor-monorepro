using System.Threading;
using System.Threading.Tasks;

namespace ScheduledTasks
{
    public interface IScheduledTask
    {
        string Schedule { get; }
        Task ExecuteAsync(CancellationToken cancellationToken);
    }
}