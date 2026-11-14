using LifeFitAI.Application.Fitness.Assessment;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Fitness.Ai;

internal static class WorkoutMonthGeneration
{
    public static async Task<Result<ProgramResponse>> Create(
        IOperationGuard guard,
        ICurrentActor actor,
        IAiProvider provider,
        IAiLimits limits,
        IAiSafetyService safety,
        IAiQuotaService quota,
        IAiRepository ai,
        IFitnessRepository fitness,
        IWorkoutRepository workouts,
        ITenantRepository tenants,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        GenerateWorkoutCommand request,
        CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(guard, actor, PermissionCodes.AiUse, FeatureCodes.AiWorkoutGeneration, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<ProgramResponse>(scope.Error);
        }

        var coach = await guard.AllowAsync(PermissionCodes.AiUse, FeatureCodes.AiCoach, cancellationToken).ConfigureAwait(false);
        if (coach.IsFailure)
        {
            return Result.Failure<ProgramResponse>(coach.Error);
        }

        var tenant = await tenants.GetAsync(scope.Value.TenantId, cancellationToken).ConfigureAwait(false);
        if (tenant is not null && RoleNames.IsBusiness(tenant.TenantType))
        {
            var monthStart = new DateTime(clock.UtcNow.Year, clock.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var generated = await ai.CountUsageAsync(scope.Value.TenantId, scope.Value.UserId, AiRequestType.WorkoutGeneration, monthStart, cancellationToken).ConfigureAwait(false);
            if (generated > 0)
            {
                return Result.Failure<ProgramResponse>(AiErrors.WorkoutPlanMonthlyLimit);
            }
        }

        var quotaResult = await quota.AllowAsync(scope.Value.UserId, scope.Value.TenantId, cancellationToken).ConfigureAwait(false);
        var notes = string.IsNullOrWhiteSpace(request.Notes) ? "Generate a 30-day workout plan." : request.Notes.Trim();
        var input = safety.Screen(notes);
        if (quotaResult.IsFailure || input.IsFailure)
        {
            return Result.Failure<ProgramResponse>(quotaResult.IsFailure ? quotaResult.Error : input.Error);
        }

        var today = DateOnly.FromDateTime(clock.UtcNow);
        var assessment = await fitness.GetAssessmentAsync(scope.Value.TenantId, scope.Value.UserId, cancellationToken).ConfigureAwait(false);
        var briefResult = AssessmentWorkoutBrief.Create(assessment, today);
        if (briefResult.IsFailure)
        {
            return Result.Failure<ProgramResponse>(briefResult.Error);
        }

        var catalog = await fitness.ListGenerationExercisesAsync(scope.Value.TenantId, cancellationToken).ConfigureAwait(false);
        var workout = AssessmentWorkoutBrief.Read<AssessmentWorkoutSection>(assessment!.WorkoutJson);
        var matched = AssessmentWorkoutBrief.MatchEquipment(catalog, workout?.Equipment, workout?.Location);
        if (matched.Count == 0)
        {
            return Result.Failure<ProgramResponse>(AiErrors.NoMatchingExercises);
        }

        var userMessage = AssessmentWorkoutBrief.ToJson(briefResult.Value, matched.Select(item => item.Name).ToArray());
        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            userMessage += "\nAdditional notes: " + request.Notes.Trim();
        }

        var system = FitnessPrompts.WorkoutMonthSystem();
        var completion = await provider.CompleteAsync(
            new AiCompletionRequest(limits.Model, [new("system", system.Length <= limits.MaxPromptCharacters ? system : system[..limits.MaxPromptCharacters]), new("user", userMessage.Length <= 14000 ? userMessage : userMessage[..14000])], 12000),
            cancellationToken).ConfigureAwait(false);
        if (!completion.Succeeded || string.IsNullOrWhiteSpace(completion.Content))
        {
            var error = completion.FailureCode switch
            {
                "quota" => AiErrors.Quota,
                "invalid" => AiErrors.InvalidOutput,
                _ => AiErrors.Unavailable,
            };
            return Result.Failure<ProgramResponse>(error);
        }

        if (safety.Screen(completion.Content).IsFailure)
        {
            return Result.Failure<ProgramResponse>(AiErrors.Safety);
        }

        var allowed = matched.Select(item => item.Id).ToHashSet();
        var names = matched.GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.First().Id, StringComparer.OrdinalIgnoreCase);
        var parsed = WorkoutPlanParser.ParseMonth(completion.Content, allowed, names);
        if (parsed.IsFailure)
        {
            return Result.Failure<ProgramResponse>(parsed.Error);
        }

        var ids = matched.ToDictionary(item => item.Id, item => item.Name);
        var rules = WorkoutMonthRules.Validate(parsed.Value, briefResult.Value.DaysPerWeek, ids, briefResult.Value.Avoid);
        if (rules.IsFailure)
        {
            return Result.Failure<ProgramResponse>(rules.Error);
        }

        return await SaveAsync(scope.Value, briefResult.Value, parsed.Value, completion, limits, ai, workouts, unitOfWork, clock, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<ProgramResponse>> SaveAsync(
        (Guid UserId, Guid TenantId) scope,
        WorkoutBrief brief,
        GeneratedProgram plan,
        AiCompletionResult completion,
        IAiLimits limits,
        IAiRepository ai,
        IWorkoutRepository workouts,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        CancellationToken cancellationToken)
    {
        ProgramResponse? created = null;
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var training = plan.Days.Count(day => !day.RestDay);
            var frequency = Math.Clamp((int)Math.Round(training * 7m / 30m, MidpointRounding.AwayFromZero), 1, 7);
            var name = string.IsNullOrWhiteSpace(plan.Name) ? "30-day plan" : plan.Name.Trim();
            var program = WorkoutProgram.Create(
                Guid.NewGuid(),
                scope.TenantId,
                scope.UserId,
                name.Length <= 200 ? name : name[..200],
                $"30-day plan from assessment {brief.AssessmentId:N}. Prompt {FitnessPrompts.WorkoutMonthVersion}.",
                AssessmentMaps.MapGoal(brief.PrimaryGoal),
                MapDifficulty(brief.Level),
                5,
                frequency);
            program.AttachGeneration(brief.AssessmentId, FitnessPrompts.WorkoutMonthVersion, completion.Model ?? limits.Model, brief.PlanStart);
            await workouts.AddProgramAsync(program, token).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(token).ConfigureAwait(false);
            foreach (var dayInput in plan.Days)
            {
                var date = brief.PlanStart.AddDays(dayInput.DayNumber - 1).ToString("yyyy-MM-dd");
                var description = string.IsNullOrWhiteSpace(dayInput.Description) ? date : $"{date}. {dayInput.Description}";
                var day = WorkoutProgramDay.Create(Guid.NewGuid(), program.Id, dayInput.DayNumber, dayInput.Name, description.Length <= 1000 ? description : description[..1000], dayInput.RestDay);
                await workouts.AddDayAsync(day, token).ConfigureAwait(false);
                await unitOfWork.SaveChangesAsync(token).ConfigureAwait(false);
                var sequence = 1;
                foreach (var item in dayInput.Exercises)
                {
                    var exerciseNotes = item.Notes;
                    await workouts.AddProgramExerciseAsync(
                        WorkoutProgramExercise.Create(Guid.NewGuid(), day.Id, item.ExerciseId, sequence++, item.Sets, item.TargetReps, item.TargetDurationSeconds, null, null, item.RestSeconds, exerciseNotes),
                        token).ConfigureAwait(false);
                }
            }

            await ai.AddUsageAsync(AiUsageRecord.Create(Guid.NewGuid(), scope.UserId, scope.TenantId, completion.Provider ?? limits.Provider, completion.Model ?? limits.Model, AiRequestType.WorkoutGeneration, completion.InputTokens, completion.OutputTokens, limits.CostPerThousandTokens, clock.UtcNow), token).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(token).ConfigureAwait(false);
            created = await workouts.GetProgramAsync(scope.TenantId, scope.UserId, program.Id, token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
        return created is null ? Result.Failure<ProgramResponse>(AiErrors.InvalidOutput) : Result.Success(created);
    }

    private static WorkoutDifficulty MapDifficulty(string level) => level.ToLowerInvariant() switch
    {
        "advanced" => WorkoutDifficulty.Advanced,
        "intermediate" => WorkoutDifficulty.Intermediate,
        _ => WorkoutDifficulty.Beginner,
    };
}
