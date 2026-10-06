using FluentValidation;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Fitness.Ai;

public sealed record CoachChatCommand(string Message, Guid? ConversationId) : IRequest<Result<AiChatResponse>>;

public sealed class CoachChatCommandValidator : AbstractValidator<CoachChatCommand>
{
    public CoachChatCommandValidator() => RuleFor(command => command.Message).NotEmpty().MaximumLength(2000);
}

public sealed record GenerateWorkoutCommand(string? Notes) : IRequest<Result<ProgramResponse>>;

public sealed record NutritionAdviceCommand(string? Question) : IRequest<Result<AiChatResponse>>;

public sealed record ProgressInsightCommand : IRequest<Result<AiChatResponse>>;

public sealed class AiCommandHandler :
    IRequestHandler<CoachChatCommand, Result<AiChatResponse>>,
    IRequestHandler<GenerateWorkoutCommand, Result<ProgramResponse>>,
    IRequestHandler<NutritionAdviceCommand, Result<AiChatResponse>>,
    IRequestHandler<ProgressInsightCommand, Result<AiChatResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IAiProvider _provider;
    private readonly IAiLimits _limits;
    private readonly IAiSafetyService _safety;
    private readonly IAiQuotaService _quota;
    private readonly IAiContextBuilder _context;
    private readonly IAiRepository _ai;
    private readonly IFitnessRepository _fitness;
    private readonly IWorkoutRepository _workouts;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public AiCommandHandler(IOperationGuard guard, ICurrentActor actor, IAiProvider provider, IAiLimits limits, IAiSafetyService safety, IAiQuotaService quota, IAiContextBuilder context, IAiRepository ai, IFitnessRepository fitness, IWorkoutRepository workouts, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _provider = provider;
        _limits = limits;
        _safety = safety;
        _quota = quota;
        _context = context;
        _ai = ai;
        _fitness = fitness;
        _workouts = workouts;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public Task<Result<AiChatResponse>> Handle(CoachChatCommand request, CancellationToken cancellationToken) =>
        Chat(PermissionCodes.AiUse, FeatureCodes.AiCoach, AiRequestType.Coach, request.Message, request.ConversationId, context => FitnessPrompts.Coach(context), false, cancellationToken);

    public Task<Result<AiChatResponse>> Handle(NutritionAdviceCommand request, CancellationToken cancellationToken) =>
        Chat(PermissionCodes.AiUse, FeatureCodes.AiNutrition, AiRequestType.NutritionAdvice, string.IsNullOrWhiteSpace(request.Question) ? "Suggest a day of eating that fits my targets." : request.Question, null, FitnessPrompts.Nutrition, true, cancellationToken);

    public Task<Result<AiChatResponse>> Handle(ProgressInsightCommand request, CancellationToken cancellationToken) =>
        Chat(PermissionCodes.AiUse, FeatureCodes.AiProgressInsights, AiRequestType.ProgressInsight, "Summarize my recent training consistency.", null, FitnessPrompts.Progress, false, cancellationToken);

    public async Task<Result<ProgramResponse>> Handle(GenerateWorkoutCommand request, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.AiUse, FeatureCodes.AiWorkoutGeneration, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<ProgramResponse>(scope.Error);
        }

        var coach = await _guard.AllowAsync(PermissionCodes.AiUse, FeatureCodes.AiCoach, cancellationToken).ConfigureAwait(false);
        if (coach.IsFailure)
        {
            return Result.Failure<ProgramResponse>(coach.Error);
        }

        var quota = await _quota.AllowAsync(scope.Value.UserId, scope.Value.TenantId, cancellationToken).ConfigureAwait(false);
        var input = _safety.Screen(request.Notes ?? "Generate a workout.");
        if (quota.IsFailure || input.IsFailure)
        {
            return Result.Failure<ProgramResponse>(quota.IsFailure ? quota.Error : input.Error);
        }

        var context = await _context.BuildAsync(scope.Value.UserId, scope.Value.TenantId, cancellationToken).ConfigureAwait(false);
        var options = await _fitness.ListExerciseOptionsAsync(scope.Value.TenantId, cancellationToken).ConfigureAwait(false);
        var allowed = options.Select(item => item.Id).ToHashSet();
        var completion = await CompleteAsync(FitnessPrompts.Workout(context, options), request.Notes, cancellationToken, maxTokens: 2000, trim: false).ConfigureAwait(false);
        if (completion.IsFailure)
        {
            return Result.Failure<ProgramResponse>(completion.Error);
        }

        var names = options.GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.First().Id, StringComparer.OrdinalIgnoreCase);
        var parsed = WorkoutPlanParser.Parse(completion.Value.Content, allowed, names);
        var output = _safety.Screen(completion.Value.Content);
        if (parsed.IsFailure || output.IsFailure)
        {
            return Result.Failure<ProgramResponse>(parsed.IsFailure ? parsed.Error : output.Error);
        }

        ProgramResponse? created = null;
        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var program = WorkoutProgram.Create(Guid.NewGuid(), scope.Value.TenantId, scope.Value.UserId, parsed.Value.Name, "Generated from validated exercise ids.", Enum.TryParse<FitnessGoalType>(context.Goal, out var goal) ? goal : FitnessGoalType.GeneralFitness, WorkoutDifficulty.Beginner, 4, Math.Clamp(parsed.Value.Days.Count(day => !day.RestDay), 1, 7));
            await _workouts.AddProgramAsync(program, token).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(token).ConfigureAwait(false);
            foreach (var dayInput in parsed.Value.Days.OrderBy(day => day.DayNumber))
            {
                var day = WorkoutProgramDay.Create(Guid.NewGuid(), program.Id, dayInput.DayNumber, dayInput.Name, null, dayInput.RestDay);
                await _workouts.AddDayAsync(day, token).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync(token).ConfigureAwait(false);
                var sequence = 1;
                foreach (var item in dayInput.Exercises)
                {
                    await _workouts.AddProgramExerciseAsync(WorkoutProgramExercise.Create(Guid.NewGuid(), day.Id, item.ExerciseId, sequence++, item.Sets, item.TargetReps, item.TargetDurationSeconds, null, null, item.RestSeconds, null), token).ConfigureAwait(false);
                }
            }

            await RecordUsage(scope.Value, AiRequestType.WorkoutGeneration, completion.Value, token).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(token).ConfigureAwait(false);
            created = await _workouts.GetProgramAsync(scope.Value.TenantId, scope.Value.UserId, program.Id, token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
        return created is null ? Result.Failure<ProgramResponse>(AiErrors.InvalidOutput) : Result.Success(created);
    }

    private async Task<Result<AiChatResponse>> Chat(string permission, string feature, AiRequestType type, string message, Guid? conversationId, Func<AiUserContext, string> prompt, bool nutritionDisclaimer, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, permission, feature, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<AiChatResponse>(scope.Error);
        }

        var quota = await _quota.AllowAsync(scope.Value.UserId, scope.Value.TenantId, cancellationToken).ConfigureAwait(false);
        var input = _safety.Screen(message);
        if (quota.IsFailure || input.IsFailure)
        {
            return Result.Failure<AiChatResponse>(quota.IsFailure ? quota.Error : input.Error);
        }

        var conversation = conversationId is Guid existing
            ? await _ai.GetConversationAsync(scope.Value.TenantId, scope.Value.UserId, existing, cancellationToken).ConfigureAwait(false)
            : null;
        if (conversationId is not null && conversation is null)
        {
            return Result.Failure<AiChatResponse>(FitnessErrors.NotFound);
        }

        var context = await _context.BuildAsync(scope.Value.UserId, scope.Value.TenantId, cancellationToken).ConfigureAwait(false);
        var history = conversation is null ? [] : await _ai.ListRecentMessagesAsync(conversation.Id, 6, cancellationToken).ConfigureAwait(false);
        var completion = await CompleteAsync(prompt(context), message, cancellationToken, history).ConfigureAwait(false);
        if (completion.IsFailure)
        {
            return Result.Failure<AiChatResponse>(completion.Error);
        }

        var answer = completion.Value.Content ?? string.Empty;
        if (nutritionDisclaimer)
        {
            answer += " This is general nutrition information, not a medical diagnosis or treatment.";
        }

        if (_safety.Screen(answer).IsFailure)
        {
            return Result.Failure<AiChatResponse>(AiErrors.Safety);
        }

        conversation ??= AiConversation.Open(Guid.NewGuid(), scope.Value.UserId, scope.Value.TenantId, "New conversation");
        if (conversationId is null)
        {
            await _ai.AddConversationAsync(conversation, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        conversation.TouchTitle(message);
        await _ai.AddMessageAsync(AiMessage.Create(Guid.NewGuid(), conversation.Id, AiMessageRole.User, message, null, null, null, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _ai.AddMessageAsync(AiMessage.Create(Guid.NewGuid(), conversation.Id, AiMessageRole.Assistant, answer, completion.Value.OutputTokens, completion.Value.Provider ?? _limits.Provider, completion.Value.Model ?? _limits.Model, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await RecordUsage(scope.Value, type, completion.Value, cancellationToken).ConfigureAwait(false);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), scope.Value.TenantId, scope.Value.UserId, "ai." + type, "AiConversation", conversation.Id, null, null, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new AiChatResponse(conversation.Id, answer, completion.Value.InputTokens, completion.Value.OutputTokens));
    }

    private async Task<Result<AiCompletionResult>> CompleteAsync(string systemPrompt, string? userMessage, CancellationToken cancellationToken, IReadOnlyList<AiMessage>? history = null, int? maxTokens = null, bool trim = true)
    {
        var messages = new List<AiPromptMessage>
        {
            new("system", systemPrompt.Length <= _limits.MaxPromptCharacters ? systemPrompt : systemPrompt[.._limits.MaxPromptCharacters]),
        };
        if (history is not null)
        {
            messages.AddRange(history.Select(item => new AiPromptMessage(item.Role == AiMessageRole.User ? "user" : "assistant", item.Content)));
        }

        if (!string.IsNullOrWhiteSpace(userMessage))
        {
            messages.Add(new AiPromptMessage("user", userMessage));
        }

        var result = await _provider.CompleteAsync(new AiCompletionRequest(_limits.Model, messages, maxTokens ?? _limits.MaxTokens), cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded || string.IsNullOrWhiteSpace(result.Content))
        {
            var error = result.FailureCode switch
            {
                "quota" => AiErrors.Quota,
                "invalid" => AiErrors.InvalidOutput,
                _ => AiErrors.Unavailable,
            };
            return Result.Failure<AiCompletionResult>(error);
        }

        var content = trim && result.Content.Length > 4000 ? result.Content[..4000] : result.Content;
        return Result.Success(result with { Content = content });
    }

    private async Task RecordUsage((Guid UserId, Guid TenantId) scope, AiRequestType type, AiCompletionResult result, CancellationToken cancellationToken)
    {
        await _ai.AddUsageAsync(AiUsageRecord.Create(Guid.NewGuid(), scope.UserId, scope.TenantId, result.Provider ?? _limits.Provider, result.Model ?? _limits.Model, type, result.InputTokens, result.OutputTokens, _limits.CostPerThousandTokens, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
    }
}

public sealed record ListConversationsQuery(int Page, int PageSize) : IRequest<Result<PageResult<ConversationSummary>>>;

public sealed record GetConversationQuery(Guid Id, int Page, int PageSize) : IRequest<Result<ConversationDetailResponse>>;

public sealed class ConversationQueryHandler :
    IRequestHandler<ListConversationsQuery, Result<PageResult<ConversationSummary>>>,
    IRequestHandler<GetConversationQuery, Result<ConversationDetailResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IAiRepository _ai;

    public ConversationQueryHandler(IOperationGuard guard, ICurrentActor actor, IAiRepository ai)
    {
        _guard = guard;
        _actor = actor;
        _ai = ai;
    }

    public async Task<Result<PageResult<ConversationSummary>>> Handle(ListConversationsQuery request, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.AiUse, FeatureCodes.AiCoach, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<PageResult<ConversationSummary>>(scope.Error);
        }

        var page = await _ai.ListConversationsAsync(scope.Value.TenantId, scope.Value.UserId, new PageQuery(request.Page, request.PageSize), cancellationToken).ConfigureAwait(false);
        return Result.Success(new PageResult<ConversationSummary>(page.Items.Select(item => new ConversationSummary(item.Id, item.Title, item.UpdatedAt)).ToArray(), page.Page, page.PageSize, page.TotalCount));
    }

    public async Task<Result<ConversationDetailResponse>> Handle(GetConversationQuery request, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.AiUse, FeatureCodes.AiCoach, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<ConversationDetailResponse>(scope.Error);
        }

        var conversation = await _ai.GetConversationAsync(scope.Value.TenantId, scope.Value.UserId, request.Id, cancellationToken).ConfigureAwait(false);
        if (conversation is null)
        {
            return Result.Failure<ConversationDetailResponse>(FitnessErrors.NotFound);
        }

        var page = await _ai.ListMessagesAsync(conversation.Id, new PageQuery(request.Page, request.PageSize), cancellationToken).ConfigureAwait(false);
        return Result.Success(new ConversationDetailResponse(conversation.Id, conversation.Title, page.Items.Select(item => new ConversationMessageResponse(item.Role.ToString(), item.Content, item.CreatedAt)).ToArray()));
    }
}
