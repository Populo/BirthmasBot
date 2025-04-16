using Quartz;
using Quartz.Spi;

namespace Birthmas.Bot.Quartz;

public class BirthmasJobFactory(IServiceProvider serviceProvider) : IJobFactory
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
    {
        return _serviceProvider.GetService(bundle.JobDetail.JobType) as IJob;
    }

    public void ReturnJob(IJob job)
    {
        var disposable = job as IDisposable;
        disposable?.Dispose();
    }
}