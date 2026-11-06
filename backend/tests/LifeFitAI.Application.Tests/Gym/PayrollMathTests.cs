using FluentAssertions;
using LifeFitAI.Domain.Gym;

namespace LifeFitAI.Application.Tests.Gym;

public sealed class PayrollMathTests
{
    [Fact]
    public void Gross_And_Net_UseRoundedComponents()
    {
        var gross = PayrollMath.Gross(10000m, 1500m, 250.55m, 100m);
        gross.Should().Be(11850.55m);
        PayrollMath.Net(gross, 500.10m).Should().Be(11350.45m);
        PayrollMath.Net(100m, 250m).Should().Be(0m);
    }

    [Fact]
    public void Line_Locks_After_Approve_And_Tracks_Partial_Payments()
    {
        var line = PayrollLine.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Ada Lovelace", "S1", "Manager", 1000m, 100m, 50m, false, false, false);
        line.SetAdjustments(25m, 75m);
        line.GrossPay.Should().Be(1175m);
        line.NetPay.Should().Be(1100m);
        line.Lock();
        line.ApplyPayment(400m).Should().BeTrue();
        line.PaymentStatus.Should().Be(PayrollPaymentStatus.PartiallyPaid);
        line.ApplyPayment(700m).Should().BeTrue();
        line.PaymentStatus.Should().Be(PayrollPaymentStatus.Paid);
        line.ApplyPayment(1m).Should().BeFalse();
        Assert.Throws<InvalidOperationException>(() => line.SetAdjustments(1m, 0m));
    }

    [Fact]
    public void Run_Moves_Through_Review_Approve_And_Paid()
    {
        var run = PayrollRun.Create(Guid.NewGuid(), Guid.NewGuid(), 2026, 10, "USD", Guid.NewGuid(), DateTime.UtcNow);
        run.Submit(DateTime.UtcNow).Should().BeTrue();
        run.Status.Should().Be(PayrollRunStatus.InReview);
        run.Approve(Guid.NewGuid(), DateTime.UtcNow).Should().BeTrue();
        run.RefreshPaymentStatus(true, false);
        run.Status.Should().Be(PayrollRunStatus.PartiallyPaid);
        run.RefreshPaymentStatus(true, true);
        run.Status.Should().Be(PayrollRunStatus.Paid);
    }
}
