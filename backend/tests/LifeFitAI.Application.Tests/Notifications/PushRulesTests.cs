using FluentAssertions;
using LifeFitAI.Application.Notifications;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Platform;

namespace LifeFitAI.Application.Tests.Notifications;

public sealed class PushRulesTests
{
    [Fact]
    public void Chat_push_hides_the_message_body()
    {
        PushRules.PushBody(NotificationType.ChatMessage, "private plan details").Should().Be("You have received a new message.");
        PushRules.Channel(NotificationType.ChatMessage).Should().Be("chat_messages");
        PushRules.DataType(NotificationType.ChatMessage).Should().Be("chat.message");
    }

    [Theory]
    [InlineData(NotificationType.WorkoutReminder, "workout_reminders")]
    [InlineData(NotificationType.WorkoutPlanUpdated, "workout_updates")]
    [InlineData(NotificationType.TrainerAssigned, "account_notifications")]
    public void Channels_follow_the_notification_kind(NotificationType type, string channel)
    {
        PushRules.Channel(type).Should().Be(channel);
    }

    [Fact]
    public void A_disabled_preference_blocks_that_kind_only()
    {
        var preference = NotificationPreference.CreateDefault(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        preference.Update(false, true, true, true, true, true, true, DateTime.UtcNow);
        PushRules.Allows(preference, NotificationType.ChatMessage).Should().BeFalse();
        PushRules.Allows(preference, NotificationType.WorkoutReminder).Should().BeTrue();
        PushRules.Allows(null, NotificationType.ChatMessage).Should().BeTrue();
    }

    [Fact]
    public void Send_tickets_distinguish_acceptance_from_an_invalid_device()
    {
        var tickets = ExpoReceipts.ReadSend("""{"data":[{"status":"ok","id":"ticket-1"},{"status":"error","message":"gone","details":{"error":"DeviceNotRegistered"}}]}""");
        tickets.Should().HaveCount(2);
        tickets[0].Accepted.Should().BeTrue();
        tickets[0].TicketId.Should().Be("ticket-1");
        tickets[1].Accepted.Should().BeFalse();
        ExpoReceipts.DeviceGone(tickets[1].Error).Should().BeTrue();
    }

    [Fact]
    public void Receipts_confirm_delivery_separately_from_acceptance()
    {
        var receipts = ExpoReceipts.ReadReceipts("""{"data":{"ticket-1":{"status":"ok"},"ticket-2":{"status":"error","details":{"error":"MessageRateExceeded"}}}}""");
        receipts["ticket-1"].Accepted.Should().BeTrue();
        receipts["ticket-2"].Accepted.Should().BeFalse();
        receipts["ticket-2"].Error.Should().Be("MessageRateExceeded");
    }

    [Fact]
    public void Only_expo_token_shapes_are_accepted()
    {
        PushRules.IsExpoToken("ExponentPushToken[abc]").Should().BeTrue();
        PushRules.IsExpoToken("server-key").Should().BeFalse();
    }
}
