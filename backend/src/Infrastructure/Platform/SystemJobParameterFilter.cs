using Hangfire.Client;
using LifeFitAI.Domain.Platform;

namespace LifeFitAI.Infrastructure.Platform;

public sealed class SystemJobParameterFilter : IClientFilter
{
    public void OnCreating(CreatingContext context)
    {
        if (context.Job.Type != typeof(MembershipExpiryNotificationJob))
        {
            return;
        }

        context.SetJobParameter("JobType", SystemJobCatalog.MembershipExpiryType);
        context.SetJobParameter("Queue", SystemJobCatalog.NotificationsQueue);
        context.SetJobParameter("SourceType", "SystemJob");
        context.SetJobParameter("SourceId", SystemJobCatalog.MembershipExpiryKey);
        if (string.IsNullOrWhiteSpace(context.GetJobParameter<string>("CorrelationId")))
        {
            context.SetJobParameter("CorrelationId", Guid.NewGuid().ToString("N"));
        }
    }

    public void OnCreated(CreatedContext context)
    {
    }
}
