using FluentValidation;
using LifeFitAI.Application.Gym.Payments;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Members;

public sealed record CreateMemberCommand(
    string Email,
    string? Password,
    string FirstName,
    string LastName,
    string? Phone,
    Guid? BranchId,
    Guid? PlanId,
    DateOnly? StartDate,
    bool AutoRenew,
    DateOnly? DateOfBirth = null,
    string? Gender = null,
    string? EmergencyContactName = null,
    string? EmergencyContactPhone = null,
    string? EmergencyContactRelationship = null,
    string? Address = null,
    string? City = null,
    string? State = null,
    string? PostalCode = null,
    DateOnly? JoinDate = null,
    string? FitnessGoal = null,
    Guid? AssignedTrainerId = null,
    ProfileStatus Status = ProfileStatus.Active,
    bool AssignAppAccess = false,
    Guid? AppSubscriptionId = null,
    bool EnableBasicAppAccess = false,
    InitialMembershipPayment? InitialPayment = null,
    Guid? PtPackageId = null,
    bool SendActivationLink = false) : IRequest<Result<MemberResponse>>;

public sealed class CreateMemberCommandValidator : AbstractValidator<CreateMemberCommand>
{
    public CreateMemberCommandValidator()
    {
        RuleFor(command => command.Email).EmailAddress().When(command => !string.IsNullOrWhiteSpace(command.Email));
        RuleFor(command => command.Email).NotEmpty().When(command => command.AssignAppAccess || command.EnableBasicAppAccess);
        RuleFor(command => command.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.LastName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Phone).NotEmpty().Must(MemberFieldRules.Phone);
        RuleFor(command => command.Gender).Must(MemberFieldRules.Gender);
        RuleFor(command => command.EmergencyContactName).MaximumLength(200);
        RuleFor(command => command.EmergencyContactPhone).Must(MemberFieldRules.Phone);
        RuleFor(command => command.EmergencyContactRelationship).MaximumLength(64);
        RuleFor(command => command.Address).MaximumLength(300);
        RuleFor(command => command.FitnessGoal).Must(goal => MemberMaps.TryGoal(goal, out _));
        RuleFor(command => command.DateOfBirth).Must(date => date is null || date <= DateOnly.FromDateTime(DateTime.UtcNow));
        RuleFor(command => command.JoinDate).NotNull().Must(date => date <= DateOnly.FromDateTime(DateTime.UtcNow));
        RuleFor(command => command.PlanId).NotEmpty().When(command => command.BranchId.HasValue);
        RuleFor(command => command.AppSubscriptionId).NotEmpty().When(command => command.AssignAppAccess);
        RuleFor(command => command.PlanId).NotEmpty().When(command => command.InitialPayment is not null);
        RuleFor(command => command.InitialPayment!.Amount).Must(MembershipPaymentRules.Amount).When(command => command.InitialPayment is not null);
        RuleFor(command => command.InitialPayment!.ReferenceNumber)
            .NotEmpty()
            .When(command => command.InitialPayment is not null && MembershipPaymentRules.RequiresReference(command.InitialPayment.PaymentMethod));
    }
}
