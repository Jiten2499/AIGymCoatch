using FluentValidation;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Branches;

public sealed record ListBranchesQuery(int Page, int PageSize) : IRequest<Result<PageResult<BranchResponse>>>;

public sealed class ListBranchesQueryHandler : IRequestHandler<ListBranchesQuery, Result<PageResult<BranchResponse>>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IBranchRepository _branches;

    public ListBranchesQueryHandler(IOperationGuard guard, ICurrentActor actor, IBranchRepository branches)
    {
        _guard = guard;
        _actor = actor;
        _branches = branches;
    }

    public async Task<Result<PageResult<BranchResponse>>> Handle(ListBranchesQuery request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.BranchRead, FeatureCodes.BranchManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<PageResult<BranchResponse>>(gate.Error);
        }

        return Result.Success(await _branches.ListAsync(tenantId, new PageQuery(request.Page, request.PageSize), cancellationToken).ConfigureAwait(false));
    }
}

public sealed record GetBranchQuery(Guid Id) : IRequest<Result<BranchResponse>>;

public sealed class GetBranchQueryHandler : IRequestHandler<GetBranchQuery, Result<BranchResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IBranchRepository _branches;

    public GetBranchQueryHandler(IOperationGuard guard, ICurrentActor actor, IBranchRepository branches)
    {
        _guard = guard;
        _actor = actor;
        _branches = branches;
    }

    public async Task<Result<BranchResponse>> Handle(GetBranchQuery request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.BranchRead, FeatureCodes.BranchManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<BranchResponse>(gate.Error);
        }

        var branch = await _branches.GetAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        return branch is null ? Result.Failure<BranchResponse>(GymErrors.NotFound) : Result.Success(Map(branch));
    }

    internal static BranchResponse Map(Branch branch) =>
        new(branch.Id, branch.Name, branch.Code, branch.Address, branch.City, branch.State, branch.Country, branch.PostalCode, branch.Phone, branch.Email, branch.TimeZone, branch.Status);
}

public sealed record CreateBranchCommand(string Name, string Code, string? Address, string? City, string? State, string? Country, string? PostalCode, string? Phone, string? Email, string TimeZone) : IRequest<Result<BranchResponse>>;

public sealed class CreateBranchCommandValidator : AbstractValidator<CreateBranchCommand>
{
    public CreateBranchCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Code).NotEmpty().MaximumLength(32);
        RuleFor(command => command.TimeZone).NotEmpty().MaximumLength(64);
    }
}

public sealed class CreateBranchCommandHandler : IRequestHandler<CreateBranchCommand, Result<BranchResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IBranchRepository _branches;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public CreateBranchCommandHandler(IOperationGuard guard, ICurrentActor actor, IBranchRepository branches, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _branches = branches;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<BranchResponse>> Handle(CreateBranchCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.BranchCreate, FeatureCodes.BranchManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<BranchResponse>(gate.Error);
        }

        var code = request.Code.Trim().ToUpperInvariant();
        if (await _branches.CodeExistsAsync(tenantId, code, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<BranchResponse>(GymErrors.Conflict);
        }

        var branch = Branch.Create(Guid.NewGuid(), tenantId, request.Name.Trim(), code, request.TimeZone);
        branch.Update(request.Name.Trim(), request.Address, request.City, request.State, request.Country, request.PostalCode, request.Phone, request.Email, request.TimeZone);
        await _branches.AddAsync(branch, cancellationToken).ConfigureAwait(false);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, _actor.UserId, "CreateBranch", "Branch", branch.Id, null, branch.Code, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(GetBranchQueryHandler.Map(branch));
    }
}

public sealed record UpdateBranchCommand(Guid Id, string Name, string? Address, string? City, string? State, string? Country, string? PostalCode, string? Phone, string? Email, string TimeZone) : IRequest<Result<BranchResponse>>;

public sealed class UpdateBranchCommandValidator : AbstractValidator<UpdateBranchCommand>
{
    public UpdateBranchCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.TimeZone).NotEmpty();
    }
}

public sealed class UpdateBranchCommandHandler : IRequestHandler<UpdateBranchCommand, Result<BranchResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IBranchRepository _branches;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public UpdateBranchCommandHandler(IOperationGuard guard, ICurrentActor actor, IBranchRepository branches, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _branches = branches;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<BranchResponse>> Handle(UpdateBranchCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.BranchUpdate, FeatureCodes.BranchManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<BranchResponse>(gate.Error);
        }

        var branch = await _branches.GetAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (branch is null)
        {
            return Result.Failure<BranchResponse>(GymErrors.NotFound);
        }

        branch.Update(request.Name.Trim(), request.Address, request.City, request.State, request.Country, request.PostalCode, request.Phone, request.Email, request.TimeZone);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, _actor.UserId, "UpdateBranch", "Branch", branch.Id, null, branch.Code, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(GetBranchQueryHandler.Map(branch));
    }
}

public sealed record DeactivateBranchCommand(Guid Id) : IRequest<Result>;

public sealed class DeactivateBranchCommandHandler : IRequestHandler<DeactivateBranchCommand, Result>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IBranchRepository _branches;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public DeactivateBranchCommandHandler(IOperationGuard guard, ICurrentActor actor, IBranchRepository branches, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _branches = branches;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result> Handle(DeactivateBranchCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.BranchDelete, FeatureCodes.BranchManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return gate;
        }

        var branch = await _branches.GetAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (branch is null)
        {
            return Result.Failure(GymErrors.NotFound);
        }

        branch.Deactivate();
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, _actor.UserId, "DeactivateBranch", "Branch", branch.Id, null, branch.Code, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}
