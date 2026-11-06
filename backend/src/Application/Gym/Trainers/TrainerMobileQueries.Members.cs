using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Gym.Trainers;

public sealed partial class TrainerMobileQueryHandlers
{
    public async Task<Result<IReadOnlyList<TrainerAlertItem>>> Handle(ListMyTrainerAlertsQuery request, CancellationToken cancellationToken)
    {
        var scope = await Scope(cancellationToken).ConfigureAwait(false);
        if (scope is null)
        {
            return Result.Failure<IReadOnlyList<TrainerAlertItem>>(IdentityErrors.Forbidden);
        }

        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var alerts = new List<TrainerAlertItem>();
        var upcoming = await Sessions(scope.Value, today, today.AddDays(2), cancellationToken).ConfigureAwait(false);
        alerts.AddRange(upcoming.Where(item => item.Status == nameof(PtSessionStatus.Scheduled)).Take(8).Select(item => new TrainerAlertItem("Session", item.MemberName, item.SessionType ?? "Session", item.ScheduledStartUtc, item.Id, item.MemberId)));
        var assignments = await _store.RecentAssignmentsAsync(scope.Value.TenantId, scope.Value.TrainerId, today.AddDays(-7), cancellationToken).ConfigureAwait(false);
        var names = await _store.MemberNamesAsync(scope.Value.TenantId, assignments.Select(item => item.MemberProfileId).ToArray(), cancellationToken).ConfigureAwait(false);
        alerts.AddRange(assignments.Select(item => new TrainerAlertItem("Assignment", names.GetValueOrDefault(item.MemberProfileId) ?? "Member", "New member assigned", TrainerMobileMap.StartOf(item.StartDate), null, item.MemberProfileId)));
        var sold = await _store.SoldPackagesAsync(scope.Value.TenantId, scope.Value.TrainerId, cancellationToken).ConfigureAwait(false);
        alerts.AddRange(sold.Where(item => item.Status == MemberPtAssignmentStatus.Active && item.EndDate >= today && item.EndDate <= today.AddDays(14)).Select(item => new TrainerAlertItem("Package", item.PackageName, "Package expires soon", TrainerMobileMap.StartOf(item.EndDate), null, item.MemberProfileId)));
        var rewards = await _store.ListRewardsAsync(scope.Value.TenantId, scope.Value.TrainerId, cancellationToken).ConfigureAwait(false);
        alerts.AddRange(rewards.Take(8).Select(item => new TrainerAlertItem("Credit", $"{item.Points} credits", item.Reason ?? "Coach credit", item.CreatedAt, null, null)));
        return Result.Success<IReadOnlyList<TrainerAlertItem>>(alerts.OrderByDescending(item => item.AtUtc).Take(30).ToArray());
    }

    public async Task<Result<IReadOnlyList<TrainerMemberItem>>> Handle(ListMyTrainerMembersQuery request, CancellationToken cancellationToken)
    {
        var scope = await Scope(cancellationToken).ConfigureAwait(false);
        if (scope is null)
        {
            return Result.Failure<IReadOnlyList<TrainerMemberItem>>(IdentityErrors.Forbidden);
        }

        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var members = await _store.ListAssignedMembersAsync(scope.Value.TenantId, scope.Value.TrainerId, cancellationToken).ConfigureAwait(false);
        var search = request.Search?.Trim();
        var rows = new List<TrainerMemberItem>();
        foreach (var member in members)
        {
            var name = $"{member.FirstName} {member.LastName}".Trim();
            if (!string.IsNullOrWhiteSpace(search) && !name.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var package = TrainerMobileMap.CurrentPackage(await _store.ListMemberPackagesAsync(scope.Value.TenantId, member.Id, cancellationToken).ConfigureAwait(false), today);
            var expiring = package is not null && package.EndDate <= today.AddDays(14);
            rows.Add(new TrainerMemberItem(member.Id, name, member.FitnessGoal?.ToString(), member.Status.ToString(), package?.PackageName, package is null ? null : package.SessionCount - package.SessionsRemaining, package?.SessionCount, package?.EndDate, expiring, member.PhotoStorageKey is not null));
        }

        return Result.Success<IReadOnlyList<TrainerMemberItem>>(rows);
    }

    public async Task<Result<TrainerMemberDetailItem>> Handle(GetMyTrainerMemberQuery request, CancellationToken cancellationToken)
    {
        var scope = await Scope(cancellationToken).ConfigureAwait(false);
        if (scope is null)
        {
            return Result.Failure<TrainerMemberDetailItem>(IdentityErrors.Forbidden);
        }

        var today = DateOnly.FromDateTime(_clock.UtcNow);
        if (!await _store.IsAssignedAsync(scope.Value.TenantId, scope.Value.TrainerId, request.MemberId, today, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<TrainerMemberDetailItem>(GymErrors.NotFound);
        }

        var member = await _store.GetMemberAsync(scope.Value.TenantId, request.MemberId, cancellationToken).ConfigureAwait(false);
        if (member is null)
        {
            return Result.Failure<TrainerMemberDetailItem>(GymErrors.NotFound);
        }

        var packages = await _store.ListMemberPackagesAsync(scope.Value.TenantId, member.Id, cancellationToken).ConfigureAwait(false);
        var packageRows = packages.Select(item => new TrainerPackageItem(item.PackageName, item.Status.ToString(), item.SessionsRemaining, item.SessionCount, item.EndDate, item.Price, item.Currency, item.Status == MemberPtAssignmentStatus.Active && item.EndDate <= today.AddDays(14))).ToArray();
        var sessions = await _store.ListSessionsAsync(scope.Value.TenantId, scope.Value.TrainerId, TrainerMobileMap.StartOf(today.AddDays(-180)), TrainerMobileMap.StartOf(today.AddDays(60)), cancellationToken).ConfigureAwait(false);
        var mine = sessions.Where(item => item.MemberProfileId == member.Id).ToArray();
        var mapped = await Map(mine, scope.Value, cancellationToken).ConfigureAwait(false);
        var notes = mine.Where(item => !string.IsNullOrWhiteSpace(item.Notes)).Select(item => new TrainerNoteItem(item.Id, item.CompletedAtUtc ?? item.ScheduledStartUtc, item.Notes!)).ToArray();
        var measurements = member.UserId is Guid userId
            ? (await _store.ListMeasurementsAsync(scope.Value.TenantId, userId, cancellationToken).ConfigureAwait(false)).Select(item => new TrainerMeasurementItem(item.MeasurementDate, item.Weight, item.BodyFatPercentage, item.Waist)).ToArray()
            : [];
        return Result.Success(new TrainerMemberDetailItem(member.Id, $"{member.FirstName} {member.LastName}".Trim(), member.Phone, member.FitnessGoal?.ToString(), member.Status.ToString(), member.JoinDate, packageRows, mapped, notes, measurements, member.PhotoStorageKey is not null));
    }

    public async Task<Result<MyTrainerPerformanceItem>> Handle(GetMyTrainerPerformanceQuery request, CancellationToken cancellationToken)
    {
        if (request.To < request.From)
        {
            return Result.Failure<MyTrainerPerformanceItem>(GymErrors.InvalidState);
        }

        var scope = await Scope(cancellationToken).ConfigureAwait(false);
        if (scope is null)
        {
            return Result.Failure<MyTrainerPerformanceItem>(IdentityErrors.Forbidden);
        }

        var dates = await _store.CompletedDatesAsync(scope.Value.TenantId, scope.Value.TrainerId, cancellationToken).ConfigureAwait(false);
        var sales = await _store.SoldPackagesAsync(scope.Value.TenantId, scope.Value.TrainerId, cancellationToken).ConfigureAwait(false);
        var clients = await _store.ClientStartsAsync(scope.Value.TenantId, scope.Value.TrainerId, cancellationToken).ConfigureAwait(false);
        var targets = await _store.ListTargetsAsync(scope.Value.TenantId, scope.Value.TrainerId, cancellationToken).ConfigureAwait(false);
        var rewards = await _store.ListRewardsAsync(scope.Value.TenantId, scope.Value.TrainerId, cancellationToken).ConfigureAwait(false);
        var periodSales = sales.Where(item => item.StartDate >= request.From && item.StartDate <= request.To).ToArray();
        var metrics = targets.Select(item =>
        {
            var actual = item.SnapshotMetric switch
            {
                TrainerTargetType.PtSessions => dates.Count(day => TrainerMobileMap.Covers(day, item.EffectiveStart, item.EffectiveEnd)),
                TrainerTargetType.PtRevenue => sales.Where(sale => TrainerMobileMap.Covers(sale.StartDate, item.EffectiveStart, item.EffectiveEnd)).Sum(sale => sale.Price),
                _ => clients.Count(day => TrainerMobileMap.Covers(day, item.EffectiveStart, item.EffectiveEnd)),
            };
            return new TrainerMetricItem(item.Id, item.SnapshotName, item.SnapshotMetric.ToString(), item.GoalValue, actual, TrainerMobileMap.Percent(actual, item.GoalValue), item.Status.ToString());
        }).ToArray();
        var credits = rewards.Take(20).Select(item => new TrainerCreditItem(item.Id, item.Points, item.TransactionType.ToString(), item.ApprovalStatus.ToString(), item.Reason, item.CreatedAt)).ToArray();
        var overall = metrics.Length == 0 ? 0 : (int)Math.Round(metrics.Average(item => item.Percent), MidpointRounding.AwayFromZero);
        return Result.Success(new MyTrainerPerformanceItem(
            TrainerRewardBalance.From(rewards),
            overall,
            dates.Count(day => day >= request.From && day <= request.To),
            periodSales.Sum(item => item.Price),
            periodSales.FirstOrDefault()?.Currency,
            periodSales.Length,
            clients.Count(day => day >= request.From && day <= request.To),
            metrics,
            credits));
    }
}
