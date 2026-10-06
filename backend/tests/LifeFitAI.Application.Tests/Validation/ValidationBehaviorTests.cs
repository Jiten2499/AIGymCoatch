using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using LifeFitAI.Application.Common.Behaviors;
using MediatR;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Validation;

public sealed class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_WhenValidatorFails_ThrowsValidationException()
    {
        var validator = Substitute.For<IValidator<SampleCommand>>();
        var failure = new ValidationFailure(nameof(SampleCommand.Name), "Name is required.");
        validator
            .ValidateAsync(Arg.Any<ValidationContext<SampleCommand>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult([failure]));

        var behavior = new ValidationBehavior<SampleCommand, string>([validator]);

        var act = async () => await behavior.Handle(
            new SampleCommand(string.Empty),
            _ => Task.FromResult("ok"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        await validator.Received(1).ValidateAsync(
            Arg.Any<ValidationContext<SampleCommand>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNoValidatorsAreRegistered_InvokesNext()
    {
        var behavior = new ValidationBehavior<SampleCommand, string>([]);
        var invoked = false;

        var result = await behavior.Handle(
            new SampleCommand("foundation"),
            _ =>
            {
                invoked = true;
                return Task.FromResult("ok");
            },
            CancellationToken.None);

        result.Should().Be("ok");
        invoked.Should().BeTrue();
    }
}

public sealed record SampleCommand(string Name) : IRequest<string>;
