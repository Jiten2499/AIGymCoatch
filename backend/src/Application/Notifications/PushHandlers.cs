using FluentValidation;
using LifeFitAI.Application.Fitness;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Domain.Platform;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Notifications;

public sealed class RegisterPushDeviceCommandValidator : AbstractValidator<RegisterPushDeviceCommand>
{
    public RegisterPushDeviceCommandValidator()
    {
        RuleFor(command => command.InstallationId).NotEmpty().MaximumLength(64).Matches("^[A-Za-z0-9-]{8,64}$");
        RuleFor(command => command.ExpoPushToken).NotEmpty().MaximumLength(200).Must(PushRules.IsExpoToken);
        RuleFor(command => command.Platform).Must(platform => platform is "android" or "ios");
        RuleFor(command => command.AppVersion).MaximumLength(32);
    }
}

public sealed class PushDeviceHandler :
    IRequestHandler<RegisterPushDeviceCommand, Result<PushDeviceResponse>>,
    IRequestHandler<DeactivatePushDeviceCommand, Result>,
    IRequestHandler<GetNotificationPreferencesQuery, Result<NotificationPreferenceResponse>>,
    IRequestHandler<UpdateNotificationPreferencesCommand, Result<NotificationPreferenceResponse>>,
    IRequestHandler<MarkAllNotificationsReadCommand, Result>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPushStore _devices;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public PushDeviceHandler(IOperationGuard guard, ICurrentActor actor, IPushStore devices, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _devices = devices;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<PushDeviceResponse>> Handle(RegisterPushDeviceCommand request, CancellationToken cancellationToken)
    {
        var scope = await OpenAsync(cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<PushDeviceResponse>(scope.Error);
        }

        var now = _clock.UtcNow;
        var device = await _devices.FindInstallationAsync(scope.Value.UserId, request.InstallationId, cancellationToken).ConfigureAwait(false);
        if (device is null || !device.IsActive)
        {
            var active = await _devices.CountActiveAsync(scope.Value.UserId, cancellationToken).ConfigureAwait(false);
            if (active >= PushRules.MaxActiveDevices)
            {
                return Result.Failure<PushDeviceResponse>(PushErrors.TooManyDevices);
            }
        }

        var holder = await _devices.FindActiveTokenAsync(request.ExpoPushToken, cancellationToken).ConfigureAwait(false);
        if (holder is not null && (holder.UserId != scope.Value.UserId || holder.InstallationId != request.InstallationId))
        {
            holder.Disable(now);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        if (device is null)
        {
            device = PushDevice.Register(Guid.NewGuid(), scope.Value.UserId, scope.Value.TenantId, request.InstallationId, request.ExpoPushToken, request.Platform, request.AppVersion, now);
            _devices.Add(device);
        }
        else
        {
            device.Refresh(scope.Value.TenantId, request.ExpoPushToken, request.Platform, request.AppVersion, now);
        }

        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), scope.Value.TenantId, scope.Value.UserId, "push.device.registered", "PushDevice", device.Id, null, request.Platform, _client.IpAddress, _client.UserAgent, now), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new PushDeviceResponse(device.Id, device.InstallationId, device.Platform, device.IsActive, device.LastRegisteredAt));
    }

    public async Task<Result> Handle(DeactivatePushDeviceCommand request, CancellationToken cancellationToken)
    {
        var scope = await OpenAsync(cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure(scope.Error);
        }

        var device = await _devices.FindInstallationAsync(scope.Value.UserId, request.InstallationId, cancellationToken).ConfigureAwait(false);
        device?.Disable(_clock.UtcNow);
        if (device is not null)
        {
            await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), scope.Value.TenantId, scope.Value.UserId, "push.device.deactivated", "PushDevice", device.Id, null, null, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }

    public async Task<Result<NotificationPreferenceResponse>> Handle(GetNotificationPreferencesQuery request, CancellationToken cancellationToken)
    {
        var scope = await OpenAsync(cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<NotificationPreferenceResponse>(scope.Error);
        }

        var preference = await _devices.FindPreferenceAsync(scope.Value.TenantId, scope.Value.UserId, cancellationToken).ConfigureAwait(false);
        return Result.Success(Map(preference));
    }

    public async Task<Result<NotificationPreferenceResponse>> Handle(UpdateNotificationPreferencesCommand request, CancellationToken cancellationToken)
    {
        var scope = await OpenAsync(cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<NotificationPreferenceResponse>(scope.Error);
        }

        var preference = await _devices.FindPreferenceAsync(scope.Value.TenantId, scope.Value.UserId, cancellationToken).ConfigureAwait(false);
        if (preference is null)
        {
            preference = NotificationPreference.CreateDefault(scope.Value.TenantId, scope.Value.UserId, _clock.UtcNow);
            _devices.Add(preference);
        }

        preference.Update(request.ChatEnabled, request.WorkoutRemindersEnabled, request.WorkoutUpdatesEnabled, request.ProgressEnabled, request.AccountEnabled, request.SoundEnabled, request.VibrationEnabled, _clock.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(Map(preference));
    }

    public async Task<Result> Handle(MarkAllNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        var scope = await OpenAsync(cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure(scope.Error);
        }

        await _devices.MarkAllReadAsync(scope.Value.TenantId, scope.Value.UserId, _clock.UtcNow, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    private async Task<Result<(Guid UserId, Guid TenantId)>> OpenAsync(CancellationToken cancellationToken)
    {
        var chat = await _guard.AllowAsync(PermissionCodes.NotificationRead, FeatureCodes.GymChat, cancellationToken).ConfigureAwait(false);
        if (chat.IsFailure)
        {
            var plans = await _guard.AllowAsync(PermissionCodes.NotificationRead, FeatureCodes.WorkoutPlans, cancellationToken).ConfigureAwait(false);
            if (plans.IsFailure)
            {
                return Result.Failure<(Guid, Guid)>(plans.Error);
            }
        }

        if (_actor.UserId is not Guid userId || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<(Guid, Guid)>(IdentityErrors.InvalidCredentials);
        }

        return Result.Success((userId, tenantId));
    }

    private static NotificationPreferenceResponse Map(NotificationPreference? preference) =>
        preference is null
            ? new NotificationPreferenceResponse(true, true, true, true, true, true, true)
            : new NotificationPreferenceResponse(preference.ChatEnabled, preference.WorkoutRemindersEnabled, preference.WorkoutUpdatesEnabled, preference.ProgressEnabled, preference.AccountEnabled, preference.SoundEnabled, preference.VibrationEnabled);
}
