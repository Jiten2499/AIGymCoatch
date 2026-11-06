using FluentValidation;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Configuration;

public sealed record UpdateMembershipConfigCommand(bool AllowOverlappingMemberships, bool AllowEarlyRenewal, bool DefaultStartOnJoinDate, int GracePeriodDays, bool AutoExpireEnabled) : IRequest<Result<MembershipConfigDto>>;

public sealed class UpdateMembershipConfigCommandValidator : AbstractValidator<UpdateMembershipConfigCommand>
{
    public UpdateMembershipConfigCommandValidator() => RuleFor(command => command.GracePeriodDays).GreaterThanOrEqualTo(0);
}

public sealed class UpdateMembershipConfigCommandHandler : IRequestHandler<UpdateMembershipConfigCommand, Result<MembershipConfigDto>>
{
    private readonly ConfigWriter _writer;

    public UpdateMembershipConfigCommandHandler(IOperationGuard guard, ICurrentActor actor, IBusinessConfigRepository configs, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client) =>
        _writer = new ConfigWriter(guard, actor, configs, audit, unitOfWork, clock, client);

    public Task<Result<MembershipConfigDto>> Handle(UpdateMembershipConfigCommand request, CancellationToken cancellationToken) =>
        _writer.UpdateMembership(request, cancellationToken);
}

public sealed record UpdateFreezeConfigCommand(int MaxFreezeDays, int MinFreezeDays, int MaxFreezeOccurrences, bool FreezeRequiresApproval, int MaxExtensionDays, bool ExtensionRequiresApproval) : IRequest<Result<FreezeConfigDto>>;

public sealed class UpdateFreezeConfigCommandValidator : AbstractValidator<UpdateFreezeConfigCommand>
{
    public UpdateFreezeConfigCommandValidator()
    {
        RuleFor(command => command.MinFreezeDays).GreaterThanOrEqualTo(0);
        RuleFor(command => command.MaxFreezeDays).GreaterThanOrEqualTo(command => command.MinFreezeDays);
        RuleFor(command => command.MaxFreezeOccurrences).GreaterThanOrEqualTo(0);
        RuleFor(command => command.MaxExtensionDays).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateFreezeConfigCommandHandler : IRequestHandler<UpdateFreezeConfigCommand, Result<FreezeConfigDto>>
{
    private readonly ConfigWriter _writer;

    public UpdateFreezeConfigCommandHandler(IOperationGuard guard, ICurrentActor actor, IBusinessConfigRepository configs, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client) =>
        _writer = new ConfigWriter(guard, actor, configs, audit, unitOfWork, clock, client);

    public Task<Result<FreezeConfigDto>> Handle(UpdateFreezeConfigCommand request, CancellationToken cancellationToken) =>
        _writer.UpdateFreeze(request, cancellationToken);
}

public sealed record UpdateAttendanceConfigCommand(int DuplicateCheckInMinutes, string? OpeningTime, string? ClosingTime, int CheckInGraceMinutes, CheckInMethod DefaultCheckInMethod) : IRequest<Result<AttendanceConfigDto>>;

public sealed class UpdateAttendanceConfigCommandValidator : AbstractValidator<UpdateAttendanceConfigCommand>
{
    public UpdateAttendanceConfigCommandValidator()
    {
        RuleFor(command => command.DuplicateCheckInMinutes).GreaterThanOrEqualTo(0);
        RuleFor(command => command.CheckInGraceMinutes).GreaterThanOrEqualTo(0);
        RuleFor(command => command.OpeningTime).Must(ConfigWriter.IsTime).When(command => !string.IsNullOrWhiteSpace(command.OpeningTime));
        RuleFor(command => command.ClosingTime).Must(ConfigWriter.IsTime).When(command => !string.IsNullOrWhiteSpace(command.ClosingTime));
    }
}

public sealed class UpdateAttendanceConfigCommandHandler : IRequestHandler<UpdateAttendanceConfigCommand, Result<AttendanceConfigDto>>
{
    private readonly ConfigWriter _writer;

    public UpdateAttendanceConfigCommandHandler(IOperationGuard guard, ICurrentActor actor, IBusinessConfigRepository configs, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client) =>
        _writer = new ConfigWriter(guard, actor, configs, audit, unitOfWork, clock, client);

    public Task<Result<AttendanceConfigDto>> Handle(UpdateAttendanceConfigCommand request, CancellationToken cancellationToken) =>
        _writer.UpdateAttendance(request, cancellationToken);
}

public sealed record UpdateCrmConfigCommand(int DefaultTrialDays, int FollowUpReminderHours, int DefaultFollowUpDays) : IRequest<Result<CrmConfigDto>>;

public sealed class UpdateCrmConfigCommandValidator : AbstractValidator<UpdateCrmConfigCommand>
{
    public UpdateCrmConfigCommandValidator()
    {
        RuleFor(command => command.DefaultTrialDays).GreaterThanOrEqualTo(0);
        RuleFor(command => command.FollowUpReminderHours).GreaterThanOrEqualTo(0);
        RuleFor(command => command.DefaultFollowUpDays).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateCrmConfigCommandHandler : IRequestHandler<UpdateCrmConfigCommand, Result<CrmConfigDto>>
{
    private readonly ConfigWriter _writer;

    public UpdateCrmConfigCommandHandler(IOperationGuard guard, ICurrentActor actor, IBusinessConfigRepository configs, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client) =>
        _writer = new ConfigWriter(guard, actor, configs, audit, unitOfWork, clock, client);

    public Task<Result<CrmConfigDto>> Handle(UpdateCrmConfigCommand request, CancellationToken cancellationToken) =>
        _writer.UpdateCrm(request, cancellationToken);
}

internal sealed class ConfigWriter
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IBusinessConfigRepository _configs;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public ConfigWriter(IOperationGuard guard, ICurrentActor actor, IBusinessConfigRepository configs, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _configs = configs;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public static bool IsTime(string? value) => TimeOnly.TryParse(value, out _);

    public async Task<Result<MembershipConfigDto>> UpdateMembership(UpdateMembershipConfigCommand request, CancellationToken cancellationToken)
    {
        var (config, error) = await LoadAsync(cancellationToken).ConfigureAwait(false);
        if (error is not null || config is null)
        {
            return Result.Failure<MembershipConfigDto>(error!);
        }

        config.UpdateMembership(request.AllowOverlappingMemberships, request.AllowEarlyRenewal, request.DefaultStartOnJoinDate, request.GracePeriodDays, request.AutoExpireEnabled);
        await SaveAsync(config.TenantId, "MembershipConfigurationUpdated", cancellationToken).ConfigureAwait(false);
        return Result.Success(new MembershipConfigDto(config.AllowOverlappingMemberships, config.AllowEarlyRenewal, config.DefaultStartOnJoinDate, config.GracePeriodDays, config.AutoExpireEnabled));
    }

    public async Task<Result<FreezeConfigDto>> UpdateFreeze(UpdateFreezeConfigCommand request, CancellationToken cancellationToken)
    {
        var (config, error) = await LoadAsync(cancellationToken).ConfigureAwait(false);
        if (error is not null || config is null)
        {
            return Result.Failure<FreezeConfigDto>(error!);
        }

        config.UpdateFreeze(request.MaxFreezeDays, request.MinFreezeDays, request.MaxFreezeOccurrences, request.FreezeRequiresApproval, request.MaxExtensionDays, request.ExtensionRequiresApproval);
        await SaveAsync(config.TenantId, "FreezeConfigurationUpdated", cancellationToken).ConfigureAwait(false);
        return Result.Success(new FreezeConfigDto(config.MaxFreezeDays, config.MinFreezeDays, config.MaxFreezeOccurrences, config.FreezeRequiresApproval, config.MaxExtensionDays, config.ExtensionRequiresApproval));
    }

    public async Task<Result<AttendanceConfigDto>> UpdateAttendance(UpdateAttendanceConfigCommand request, CancellationToken cancellationToken)
    {
        var (config, error) = await LoadAsync(cancellationToken).ConfigureAwait(false);
        if (error is not null || config is null)
        {
            return Result.Failure<AttendanceConfigDto>(error!);
        }

        TimeOnly? opening = string.IsNullOrWhiteSpace(request.OpeningTime) ? null : TimeOnly.Parse(request.OpeningTime);
        TimeOnly? closing = string.IsNullOrWhiteSpace(request.ClosingTime) ? null : TimeOnly.Parse(request.ClosingTime);
        config.UpdateAttendance(request.DuplicateCheckInMinutes, opening, closing, request.CheckInGraceMinutes, request.DefaultCheckInMethod);
        await SaveAsync(config.TenantId, "AttendanceConfigurationUpdated", cancellationToken).ConfigureAwait(false);
        return Result.Success(new AttendanceConfigDto(config.DuplicateCheckInMinutes, config.OpeningTime?.ToString("HH\\:mm"), config.ClosingTime?.ToString("HH\\:mm"), config.CheckInGraceMinutes, config.DefaultCheckInMethod));
    }

    public async Task<Result<CrmConfigDto>> UpdateCrm(UpdateCrmConfigCommand request, CancellationToken cancellationToken)
    {
        var (config, error) = await LoadAsync(cancellationToken).ConfigureAwait(false);
        if (error is not null || config is null)
        {
            return Result.Failure<CrmConfigDto>(error!);
        }

        config.UpdateCrm(request.DefaultTrialDays, request.FollowUpReminderHours, request.DefaultFollowUpDays);
        await SaveAsync(config.TenantId, "CrmConfigurationUpdated", cancellationToken).ConfigureAwait(false);
        return Result.Success(new CrmConfigDto(config.DefaultTrialDays, config.FollowUpReminderHours, config.DefaultFollowUpDays));
    }

    private async Task<(TenantBusinessConfig? Config, Error? Error)> LoadAsync(CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.ConfigurationManage, FeatureCodes.GymConfiguration, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return (null, allowed.Error);
        }

        var config = await _configs.GetConfigAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (config is null)
        {
            config = TenantBusinessConfig.CreateDefault(Guid.NewGuid(), tenantId);
            await _configs.AddConfigAsync(config, cancellationToken).ConfigureAwait(false);
        }

        return (config, null);
    }

    private async Task SaveAsync(Guid tenantId, string action, CancellationToken cancellationToken)
    {
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, _actor.UserId, action, "TenantBusinessConfig", tenantId, null, action, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
