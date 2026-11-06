using FluentValidation;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Configuration;

public sealed record CreateMemberCategoryCommand(string Name, string? Description) : IRequest<Result<MemberCategoryItem>>;
public sealed record UpdateMemberCategoryCommand(Guid Id, string Name, string? Description) : IRequest<Result<MemberCategoryItem>>;
public sealed record ChangeMemberCategoryStatusCommand(Guid Id, bool Active) : IRequest<Result<MemberCategoryItem>>;

public sealed class CreateMemberCategoryCommandValidator : AbstractValidator<CreateMemberCategoryCommand>
{
    public CreateMemberCategoryCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Description).MaximumLength(500);
    }
}

public sealed class UpdateMemberCategoryCommandValidator : AbstractValidator<UpdateMemberCategoryCommand>
{
    public UpdateMemberCategoryCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Description).MaximumLength(500);
    }
}

public sealed class CreateMemberCategoryCommandHandler : IRequestHandler<CreateMemberCategoryCommand, Result<MemberCategoryItem>>
{
    private readonly CatalogWriter _writer;

    public CreateMemberCategoryCommandHandler(IOperationGuard guard, ICurrentActor actor, IBusinessConfigRepository configs, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client) =>
        _writer = new CatalogWriter(guard, actor, configs, audit, unitOfWork, clock, client);

    public Task<Result<MemberCategoryItem>> Handle(CreateMemberCategoryCommand request, CancellationToken cancellationToken) =>
        _writer.CreateCategory(request, cancellationToken);
}

public sealed class UpdateMemberCategoryCommandHandler : IRequestHandler<UpdateMemberCategoryCommand, Result<MemberCategoryItem>>
{
    private readonly CatalogWriter _writer;

    public UpdateMemberCategoryCommandHandler(IOperationGuard guard, ICurrentActor actor, IBusinessConfigRepository configs, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client) =>
        _writer = new CatalogWriter(guard, actor, configs, audit, unitOfWork, clock, client);

    public Task<Result<MemberCategoryItem>> Handle(UpdateMemberCategoryCommand request, CancellationToken cancellationToken) =>
        _writer.UpdateCategory(request, cancellationToken);
}

public sealed class ChangeMemberCategoryStatusCommandHandler : IRequestHandler<ChangeMemberCategoryStatusCommand, Result<MemberCategoryItem>>
{
    private readonly CatalogWriter _writer;

    public ChangeMemberCategoryStatusCommandHandler(IOperationGuard guard, ICurrentActor actor, IBusinessConfigRepository configs, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client) =>
        _writer = new CatalogWriter(guard, actor, configs, audit, unitOfWork, clock, client);

    public Task<Result<MemberCategoryItem>> Handle(ChangeMemberCategoryStatusCommand request, CancellationToken cancellationToken) =>
        _writer.ChangeCategoryStatus(request, cancellationToken);
}

public sealed record CreateDiscountRuleCommand(
    string Name,
    string? Description,
    DiscountType DiscountType,
    decimal Value,
    DiscountAppliesTo AppliesTo,
    Guid? PackageId,
    PackageCategory? PackageCategory,
    Guid? MemberCategoryId,
    decimal? MinimumPackagePrice,
    int? MinimumDurationDays,
    decimal? MaximumDiscountAmount,
    DateOnly? StartDate,
    DateOnly? EndDate) : IRequest<Result<DiscountRuleItem>>;

public sealed record UpdateDiscountRuleCommand(
    Guid Id,
    string Name,
    string? Description,
    DiscountType DiscountType,
    decimal Value,
    DiscountAppliesTo AppliesTo,
    Guid? PackageId,
    PackageCategory? PackageCategory,
    Guid? MemberCategoryId,
    decimal? MinimumPackagePrice,
    int? MinimumDurationDays,
    decimal? MaximumDiscountAmount,
    DateOnly? StartDate,
    DateOnly? EndDate) : IRequest<Result<DiscountRuleItem>>;

public sealed record ChangeDiscountStatusCommand(Guid Id, bool Active) : IRequest<Result<DiscountRuleItem>>;

public sealed class CreateDiscountRuleCommandValidator : AbstractValidator<CreateDiscountRuleCommand>
{
    public CreateDiscountRuleCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Value).GreaterThan(0);
        RuleFor(command => command.Value).LessThanOrEqualTo(100).When(command => command.DiscountType == DiscountType.Percentage);
        RuleFor(command => command.EndDate).GreaterThanOrEqualTo(command => command.StartDate).When(command => command.StartDate.HasValue && command.EndDate.HasValue);
        RuleFor(command => command.PackageId).NotEmpty().When(command => command.AppliesTo == DiscountAppliesTo.SpecificPackage);
        RuleFor(command => command.PackageCategory).NotNull().When(command => command.AppliesTo == DiscountAppliesTo.PackageCategory);
        RuleFor(command => command.MemberCategoryId).NotEmpty().When(command => command.AppliesTo == DiscountAppliesTo.MemberCategory);
    }
}

public sealed class UpdateDiscountRuleCommandValidator : AbstractValidator<UpdateDiscountRuleCommand>
{
    public UpdateDiscountRuleCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Value).GreaterThan(0);
        RuleFor(command => command.Value).LessThanOrEqualTo(100).When(command => command.DiscountType == DiscountType.Percentage);
        RuleFor(command => command.EndDate).GreaterThanOrEqualTo(command => command.StartDate).When(command => command.StartDate.HasValue && command.EndDate.HasValue);
        RuleFor(command => command.PackageId).NotEmpty().When(command => command.AppliesTo == DiscountAppliesTo.SpecificPackage);
        RuleFor(command => command.PackageCategory).NotNull().When(command => command.AppliesTo == DiscountAppliesTo.PackageCategory);
        RuleFor(command => command.MemberCategoryId).NotEmpty().When(command => command.AppliesTo == DiscountAppliesTo.MemberCategory);
    }
}

public sealed class CreateDiscountRuleCommandHandler : IRequestHandler<CreateDiscountRuleCommand, Result<DiscountRuleItem>>
{
    private readonly CatalogWriter _writer;

    public CreateDiscountRuleCommandHandler(IOperationGuard guard, ICurrentActor actor, IBusinessConfigRepository configs, IMembershipRepository plans, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client) =>
        _writer = new CatalogWriter(guard, actor, configs, audit, unitOfWork, clock, client, plans);

    public Task<Result<DiscountRuleItem>> Handle(CreateDiscountRuleCommand request, CancellationToken cancellationToken) =>
        _writer.CreateDiscount(request, cancellationToken);
}

public sealed class UpdateDiscountRuleCommandHandler : IRequestHandler<UpdateDiscountRuleCommand, Result<DiscountRuleItem>>
{
    private readonly CatalogWriter _writer;

    public UpdateDiscountRuleCommandHandler(IOperationGuard guard, ICurrentActor actor, IBusinessConfigRepository configs, IMembershipRepository plans, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client) =>
        _writer = new CatalogWriter(guard, actor, configs, audit, unitOfWork, clock, client, plans);

    public Task<Result<DiscountRuleItem>> Handle(UpdateDiscountRuleCommand request, CancellationToken cancellationToken) =>
        _writer.UpdateDiscount(request, cancellationToken);
}

public sealed class ChangeDiscountStatusCommandHandler : IRequestHandler<ChangeDiscountStatusCommand, Result<DiscountRuleItem>>
{
    private readonly CatalogWriter _writer;

    public ChangeDiscountStatusCommandHandler(IOperationGuard guard, ICurrentActor actor, IBusinessConfigRepository configs, IMembershipRepository plans, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client) =>
        _writer = new CatalogWriter(guard, actor, configs, audit, unitOfWork, clock, client, plans);

    public Task<Result<DiscountRuleItem>> Handle(ChangeDiscountStatusCommand request, CancellationToken cancellationToken) =>
        _writer.ChangeDiscountStatus(request, cancellationToken);
}

public sealed record CreateCrmLeadSourceCommand(string Name) : IRequest<Result<CrmCatalogItem>>;
public sealed record ChangeCrmLeadSourceStatusCommand(Guid Id, bool Active) : IRequest<Result<CrmCatalogItem>>;
public sealed record CreateCrmLostReasonCommand(string Name) : IRequest<Result<CrmCatalogItem>>;
public sealed record ChangeCrmLostReasonStatusCommand(Guid Id, bool Active) : IRequest<Result<CrmCatalogItem>>;

public sealed class CreateCrmLeadSourceCommandValidator : AbstractValidator<CreateCrmLeadSourceCommand>
{
    public CreateCrmLeadSourceCommandValidator() => RuleFor(command => command.Name).NotEmpty().MaximumLength(100);
}

public sealed class CreateCrmLostReasonCommandValidator : AbstractValidator<CreateCrmLostReasonCommand>
{
    public CreateCrmLostReasonCommandValidator() => RuleFor(command => command.Name).NotEmpty().MaximumLength(100);
}

public sealed class CreateCrmLeadSourceCommandHandler : IRequestHandler<CreateCrmLeadSourceCommand, Result<CrmCatalogItem>>
{
    private readonly CatalogWriter _writer;

    public CreateCrmLeadSourceCommandHandler(IOperationGuard guard, ICurrentActor actor, IBusinessConfigRepository configs, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client) =>
        _writer = new CatalogWriter(guard, actor, configs, audit, unitOfWork, clock, client);

    public Task<Result<CrmCatalogItem>> Handle(CreateCrmLeadSourceCommand request, CancellationToken cancellationToken) =>
        _writer.CreateLeadSource(request, cancellationToken);
}

public sealed class ChangeCrmLeadSourceStatusCommandHandler : IRequestHandler<ChangeCrmLeadSourceStatusCommand, Result<CrmCatalogItem>>
{
    private readonly CatalogWriter _writer;

    public ChangeCrmLeadSourceStatusCommandHandler(IOperationGuard guard, ICurrentActor actor, IBusinessConfigRepository configs, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client) =>
        _writer = new CatalogWriter(guard, actor, configs, audit, unitOfWork, clock, client);

    public Task<Result<CrmCatalogItem>> Handle(ChangeCrmLeadSourceStatusCommand request, CancellationToken cancellationToken) =>
        _writer.ChangeLeadSourceStatus(request, cancellationToken);
}

public sealed class CreateCrmLostReasonCommandHandler : IRequestHandler<CreateCrmLostReasonCommand, Result<CrmCatalogItem>>
{
    private readonly CatalogWriter _writer;

    public CreateCrmLostReasonCommandHandler(IOperationGuard guard, ICurrentActor actor, IBusinessConfigRepository configs, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client) =>
        _writer = new CatalogWriter(guard, actor, configs, audit, unitOfWork, clock, client);

    public Task<Result<CrmCatalogItem>> Handle(CreateCrmLostReasonCommand request, CancellationToken cancellationToken) =>
        _writer.CreateLostReason(request, cancellationToken);
}

public sealed class ChangeCrmLostReasonStatusCommandHandler : IRequestHandler<ChangeCrmLostReasonStatusCommand, Result<CrmCatalogItem>>
{
    private readonly CatalogWriter _writer;

    public ChangeCrmLostReasonStatusCommandHandler(IOperationGuard guard, ICurrentActor actor, IBusinessConfigRepository configs, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client) =>
        _writer = new CatalogWriter(guard, actor, configs, audit, unitOfWork, clock, client);

    public Task<Result<CrmCatalogItem>> Handle(ChangeCrmLostReasonStatusCommand request, CancellationToken cancellationToken) =>
        _writer.ChangeLostReasonStatus(request, cancellationToken);
}
