using FluentValidation;
using LifeFitAI.Domain.Gym;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Communications;

public sealed record CreateCommunicationCommand(
    CommunicationType Type,
    string Title,
    string Message,
    CommunicationPriority Priority,
    CommunicationAudienceType AudienceType,
    Guid? AudiencePackageId,
    Guid? AudienceTrainerId,
    int? ExpiringWithinDays,
    IReadOnlyList<Guid>? SelectedMemberIds,
    DateTime? PublishAt,
    DateTime? ExpiresAt,
    bool PublishNow,
    IReadOnlyList<Guid>? SelectedTrainerIds = null,
    IReadOnlyList<Guid>? SelectedStaffIds = null) : IRequest<Result<CommunicationDetails>>;

public sealed record UpdateCommunicationCommand(
    Guid Id,
    CommunicationType Type,
    string Title,
    string Message,
    CommunicationPriority Priority,
    CommunicationAudienceType AudienceType,
    Guid? AudiencePackageId,
    Guid? AudienceTrainerId,
    int? ExpiringWithinDays,
    IReadOnlyList<Guid>? SelectedMemberIds,
    DateTime? PublishAt,
    DateTime? ExpiresAt,
    IReadOnlyList<Guid>? SelectedTrainerIds = null,
    IReadOnlyList<Guid>? SelectedStaffIds = null,
    bool PublishNow = false) : IRequest<Result<CommunicationDetails>>;

public sealed record PublishCommunicationCommand(Guid Id) : IRequest<Result<CommunicationDetails>>;

public sealed record ArchiveCommunicationCommand(Guid Id) : IRequest<Result<CommunicationDetails>>;

public sealed class CreateCommunicationCommandValidator : AbstractValidator<CreateCommunicationCommand>
{
    public CreateCommunicationCommandValidator()
    {
        RuleFor(command => command.Title).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Message).NotEmpty().MaximumLength(4000);
        RuleFor(command => command.AudiencePackageId).NotEmpty().When(command => command.AudienceType == CommunicationAudienceType.MembershipPackage);
        RuleFor(command => command.AudienceTrainerId).NotEmpty().When(command => command.AudienceType == CommunicationAudienceType.TrainerMembers);
        RuleFor(command => command.SelectedMemberIds).NotEmpty().When(command => command.AudienceType == CommunicationAudienceType.SelectedMembers);
        RuleFor(command => command.SelectedTrainerIds).NotEmpty().When(command => command.AudienceType == CommunicationAudienceType.SelectedTrainers);
        RuleFor(command => command.SelectedStaffIds).NotEmpty().When(command => command.AudienceType == CommunicationAudienceType.SelectedStaff);
    }
}

public sealed class UpdateCommunicationCommandValidator : AbstractValidator<UpdateCommunicationCommand>
{
    public UpdateCommunicationCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.Title).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Message).NotEmpty().MaximumLength(4000);
        RuleFor(command => command.AudiencePackageId).NotEmpty().When(command => command.AudienceType == CommunicationAudienceType.MembershipPackage);
        RuleFor(command => command.AudienceTrainerId).NotEmpty().When(command => command.AudienceType == CommunicationAudienceType.TrainerMembers);
        RuleFor(command => command.SelectedMemberIds).NotEmpty().When(command => command.AudienceType == CommunicationAudienceType.SelectedMembers);
        RuleFor(command => command.SelectedTrainerIds).NotEmpty().When(command => command.AudienceType == CommunicationAudienceType.SelectedTrainers);
        RuleFor(command => command.SelectedStaffIds).NotEmpty().When(command => command.AudienceType == CommunicationAudienceType.SelectedStaff);
    }
}
