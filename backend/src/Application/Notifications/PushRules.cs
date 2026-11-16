using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Platform;

namespace LifeFitAI.Application.Notifications;

public static class PushRules
{
    public const int MaxActiveDevices = 8;

    public static bool IsExpoToken(string token) =>
        token.StartsWith("ExponentPushToken[", StringComparison.Ordinal) || token.StartsWith("ExpoPushToken[", StringComparison.Ordinal);

    public static bool Allows(NotificationPreference? preference, NotificationType type)
    {
        if (preference is null)
        {
            return true;
        }

        return type switch
        {
            NotificationType.ChatMessage => preference.ChatEnabled,
            NotificationType.WorkoutReminder => preference.WorkoutRemindersEnabled,
            NotificationType.WorkoutCompleted or NotificationType.AiRecommendation or NotificationType.WorkoutPlanUpdated => preference.WorkoutUpdatesEnabled,
            NotificationType.ChallengeReminder or NotificationType.ProgressMilestone => preference.ProgressEnabled,
            NotificationType.MembershipReminder or NotificationType.StaffLeaveUpdate or NotificationType.TrainerAssigned => preference.AccountEnabled,
            _ => preference.AccountEnabled,
        };
    }

    public static string Channel(NotificationType type) => type switch
    {
        NotificationType.ChatMessage => "chat_messages",
        NotificationType.WorkoutReminder => "workout_reminders",
        NotificationType.WorkoutCompleted or NotificationType.AiRecommendation or NotificationType.WorkoutPlanUpdated => "workout_updates",
        _ => "account_notifications",
    };

    public static string PushBody(NotificationType type, string storedMessage) =>
        type == NotificationType.ChatMessage ? "You have received a new message." : storedMessage;

    public static string DataType(NotificationType type) => type switch
    {
        NotificationType.ChatMessage => "chat.message",
        NotificationType.WorkoutCompleted => "workout.completed",
        NotificationType.WorkoutReminder => "workout.reminder",
        NotificationType.WorkoutPlanUpdated or NotificationType.AiRecommendation => "workout.plan.updated",
        NotificationType.TrainerAssigned => "trainer.assigned",
        NotificationType.ChallengeReminder or NotificationType.ProgressMilestone => "progress.milestone",
        _ => "account.updated",
    };

    public static string? MessageId(Notification notification)
    {
        const string prefix = "chat.message:";
        return notification.DedupKey is string key && key.StartsWith(prefix, StringComparison.Ordinal) ? key[prefix.Length..] : null;
    }
}
