using FluentAssertions;
using LifeFitAI.Application.Ai;
using LifeFitAI.Application.Fitness;
using LifeFitAI.Application.Fitness.Ai;
using LifeFitAI.Application.Fitness.Exercises;
using LifeFitAI.Application.Fitness.Profile;
using LifeFitAI.Application.Fitness.Progress;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Infrastructure.Ai;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Fitness;

public sealed class FitnessAccessTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Profile_ReadsOnlyTheCurrentTenant()
    {
        var fitness = Substitute.For<IFitnessRepository>();
        fitness.GetProfileAsync(_tenantId, _userId, Arg.Any<CancellationToken>()).Returns((FitnessProfile?)null);
        var handler = new GetFitnessProfileQueryHandler(Allow(), Actor(), fitness);

        var result = await handler.Handle(new GetFitnessProfileQuery(), CancellationToken.None);

        result.Error.Code.Should().Be("fitness.not_found");
        await fitness.Received(1).GetProfileAsync(_tenantId, _userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Exercise_IsLoadedForTheCurrentTenant()
    {
        var fitness = Substitute.For<IFitnessRepository>();
        var handler = new GetExerciseQueryHandler(Allow(), Actor(), fitness);
        await handler.Handle(new GetExerciseQuery(Guid.NewGuid()), CancellationToken.None);
        await fitness.Received(1).GetExerciseAsync(_tenantId, Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Dashboard_UsesTheCurrentTenant()
    {
        var query = Substitute.For<IMemberDashboardQuery>();
        query.GetAsync(_tenantId, _userId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(EmptyDashboard());
        var handler = new GetMemberDashboardQueryHandler(Allow(), Actor(), query, Clock());

        var result = await handler.Handle(new GetMemberDashboardQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await query.Received(1).GetAsync(_tenantId, _userId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AiFeatureGate_StopsBeforeTheProvider()
    {
        var guard = Substitute.For<IOperationGuard>();
        guard.AllowAsync(PermissionCodes.AiUse, FeatureCodes.AiWorkoutGeneration, Arg.Any<CancellationToken>()).Returns(Result.Failure(GymErrors.FeatureDisabled));
        var provider = Substitute.For<IAiProvider>();
        var handler = AiHandler(guard, provider, Substitute.For<IAiRepository>());

        var result = await handler.Handle(new GenerateWorkoutCommand(null), CancellationToken.None);

        result.Error.Code.Should().Be("gym.feature_disabled");
        await provider.DidNotReceive().CompleteAsync(Arg.Any<AiCompletionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AiCoachFeature_StopsWorkoutGenerationWhenThePlanOmitsTheCoach()
    {
        var guard = Substitute.For<IOperationGuard>();
        guard.AllowAsync(PermissionCodes.AiUse, FeatureCodes.AiWorkoutGeneration, Arg.Any<CancellationToken>()).Returns(Result.Success());
        guard.AllowAsync(PermissionCodes.AiUse, FeatureCodes.AiCoach, Arg.Any<CancellationToken>()).Returns(Result.Failure(GymErrors.FeatureDisabled));
        var provider = Substitute.For<IAiProvider>();
        var handler = AiHandler(guard, provider, Substitute.For<IAiRepository>());

        var result = await handler.Handle(new GenerateWorkoutCommand(null), CancellationToken.None);

        result.Error.Code.Should().Be("gym.feature_disabled");
        await provider.DidNotReceive().CompleteAsync(Arg.Any<AiCompletionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AiQuota_BlocksWhenTheDailyLimitIsReached()
    {
        var usage = Substitute.For<IAiRepository>();
        usage.CountUsageAsync(_tenantId, _userId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(20);
        var limits = Substitute.For<IAiLimits>();
        limits.DailyRequestLimit.Returns(20);
        limits.MonthlyRequestLimit.Returns(200);
        var quota = new AiQuotaService(usage, limits, Clock());

        var result = await quota.AllowAsync(_userId, _tenantId, CancellationToken.None);

        result.Error.Code.Should().Be("ai.quota");
    }

    [Fact]
    public async Task Coach_StoresMessagesOnlyAfterASafeResponse()
    {
        var provider = Substitute.For<IAiProvider>();
        provider.CompleteAsync(Arg.Any<AiCompletionRequest>(), Arg.Any<CancellationToken>()).Returns(new AiCompletionResult(true, "Train with a moderate load.", 12, 18, string.Empty));
        var ai = Substitute.For<IAiRepository>();
        var handler = AiHandler(Allow(), provider, ai);

        var result = await handler.Handle(new CoachChatCommand("What should I train?", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Message.Should().Be("Train with a moderate load.");
        await ai.Received(2).AddMessageAsync(Arg.Any<AiMessage>(), Arg.Any<CancellationToken>());
        await ai.Received(1).AddUsageAsync(Arg.Any<AiUsageRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Coach_DoesNotStoreAnAssistantMessageWhenTheProviderFails()
    {
        var provider = Substitute.For<IAiProvider>();
        provider.CompleteAsync(Arg.Any<AiCompletionRequest>(), Arg.Any<CancellationToken>()).Returns(new AiCompletionResult(false, null, 0, 0, "timeout"));
        var ai = Substitute.For<IAiRepository>();
        var handler = AiHandler(Allow(), provider, ai);

        var result = await handler.Handle(new CoachChatCommand("What should I train?", null), CancellationToken.None);

        result.Error.Code.Should().Be("ai.unavailable");
        await ai.DidNotReceive().AddMessageAsync(Arg.Any<AiMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Coach_RejectsUnsafeInputBeforeCallingTheProvider()
    {
        var provider = Substitute.For<IAiProvider>();
        var handler = AiHandler(Allow(), provider, Substitute.For<IAiRepository>());

        var result = await handler.Handle(new CoachChatCommand("Please diagnose this pain.", null), CancellationToken.None);

        result.Error.Code.Should().Be("ai.safety");
        await provider.DidNotReceive().CompleteAsync(Arg.Any<AiCompletionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WorkoutGeneration_DoesNotPersistAnUnknownExercise()
    {
        var provider = Substitute.For<IAiProvider>();
        provider.CompleteAsync(Arg.Any<AiCompletionRequest>(), Arg.Any<CancellationToken>()).Returns(new AiCompletionResult(true, "{\"name\":\"Plan\",\"days\":[{\"dayNumber\":1,\"name\":\"Push\",\"restDay\":false,\"exercises\":[{\"exerciseId\":\"" + Guid.NewGuid() + "\",\"sets\":3}]}]}", 5, 5, string.Empty));
        var workouts = Substitute.For<IWorkoutRepository>();
        var handler = AiHandler(Allow(), provider, Substitute.For<IAiRepository>(), workouts);

        var result = await handler.Handle(new GenerateWorkoutCommand("general fitness"), CancellationToken.None);

        result.Error.Code.Should().Be("ai.invalid_output");
        await workouts.DidNotReceive().AddProgramAsync(Arg.Any<WorkoutProgram>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Conversation_IsScopedToTheCurrentTenant()
    {
        var ai = Substitute.For<IAiRepository>();
        var handler = new ConversationQueryHandler(Allow(), Actor(), ai);
        var id = Guid.NewGuid();

        var result = await handler.Handle(new GetConversationQuery(id, 1, 20), CancellationToken.None);

        result.Error.Code.Should().Be("fitness.not_found");
        await ai.Received(1).GetConversationAsync(_tenantId, _userId, id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Provider_ReturnsUnavailableWithoutAKeyAndInvalidWhenJsonIsMalformed()
    {
        var unconfigured = new OpenAiCompatibleAiProvider(Factory(new StubHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK))), Runtime(null), NullLogger<OpenAiCompatibleAiProvider>.Instance);
        var missing = await unconfigured.CompleteAsync(new AiCompletionRequest("model", [], 10), CancellationToken.None);
        missing.FailureCode.Should().Be("unavailable");

        var handler = new StubHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("{") });
        var provider = new OpenAiCompatibleAiProvider(Factory(handler), Runtime("test-key"), NullLogger<OpenAiCompatibleAiProvider>.Instance);
        var invalid = await provider.CompleteAsync(new AiCompletionRequest("model", [], 10), CancellationToken.None);
        invalid.FailureCode.Should().Be("unavailable");
    }

    [Fact]
    public async Task Provider_ReturnsASuccessfulCompletionAndATimeout()
    {
        var successHandler = new StubHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"Hello\"}}],\"usage\":{\"prompt_tokens\":3,\"completion_tokens\":4}}"),
        });
        var success = await Provider(successHandler).CompleteAsync(new AiCompletionRequest("model", [], 10), CancellationToken.None);
        success.Succeeded.Should().BeTrue();
        success.Content.Should().Be("Hello");
        success.InputTokens.Should().Be(3);

        var reasoning = new StubHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("{\"choices\":[{\"message\":{\"content\":null,\"reasoning\":\"<|channel|>analysis<|message|>draft<|channel|>final<|message|>Train three days.\"}}]}"),
        });
        var runtime = Substitute.For<ILlmRuntime>();
        runtime.ResolveAsync(Arg.Any<CancellationToken>()).Returns(new LlmCallSettings("Groq", "https://example.com/v1", "test-key", "openai/gpt-oss-120b", 20));
        var recovered = await new OpenAiCompatibleAiProvider(Factory(reasoning), runtime, NullLogger<OpenAiCompatibleAiProvider>.Instance).CompleteAsync(new AiCompletionRequest("openai/gpt-oss-120b", [], 800), CancellationToken.None);
        recovered.Succeeded.Should().BeTrue();
        recovered.Content.Should().Be("Train three days.");

        var timeout = new StubHandler(_ => throw new TaskCanceledException());
        var failed = await Provider(timeout).CompleteAsync(new AiCompletionRequest("model", [], 10), CancellationToken.None);
        failed.FailureCode.Should().Be("timeout");
    }

    private static OpenAiCompatibleAiProvider Provider(HttpMessageHandler handler) =>
        new(Factory(handler), Runtime("test-key"), NullLogger<OpenAiCompatibleAiProvider>.Instance);

    private static IHttpClientFactory Factory(HttpMessageHandler handler)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("llm").Returns(new HttpClient(handler));
        return factory;
    }

    private static ILlmRuntime Runtime(string? key) 
    {
        var runtime = Substitute.For<ILlmRuntime>();
        runtime.ResolveAsync(Arg.Any<CancellationToken>()).Returns(string.IsNullOrEmpty(key) ? null : new LlmCallSettings("OpenAiCompatible", "https://example.com/v1", key, "model", 20));
        return runtime;
    }

    private AiCommandHandler AiHandler(IOperationGuard guard, IAiProvider provider, IAiRepository ai, IWorkoutRepository? workouts = null)
    {
        var limits = Substitute.For<IAiLimits>();
        limits.Provider.Returns("Test");
        limits.Model.Returns("test-model");
        limits.MaxTokens.Returns(100);
        limits.MaxPromptCharacters.Returns(4000);
        limits.CostPerThousandTokens.Returns(0m);
        var quota = Substitute.For<IAiQuotaService>();
        quota.AllowAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        var context = Substitute.For<IAiContextBuilder>();
        context.BuildAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new AiUserContext("Strength", "Beginner", "Moderate", "Metric", 3, 45, [], 0, 2000, 0, 120, null));
        var fitness = Substitute.For<IFitnessRepository>();
        fitness.ListExerciseOptionsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new List<ExerciseOption> { new(Guid.Parse("33333333-3333-4333-8333-333333333001"), "Bench", "Strength") });
        return new AiCommandHandler(guard, Actor(), provider, limits, new AiSafetyService(), quota, context, ai, fitness, workouts ?? Substitute.For<IWorkoutRepository>(), Substitute.For<IAuditLogRepository>(), Substitute.For<IUnitOfWork>(), Clock(), Substitute.For<IClientContext>());
    }

    private static DashboardResponse EmptyDashboard() =>
        new(null, null, 0, 0, 0, [], new NutritionSummaryResponse(new DateOnly(2026, 10, 1), 0, 0, 0, 0, 0, 0, 0, 0), null, null);

    private IOperationGuard Allow()
    {
        var guard = Substitute.For<IOperationGuard>();
        guard.AllowAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        return guard;
    }

    private ICurrentActor Actor()
    {
        var actor = Substitute.For<ICurrentActor>();
        actor.UserId.Returns(_userId);
        actor.TenantId.Returns(_tenantId);
        return actor;
    }

    private IDateTimeProvider Clock()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_now);
        return clock;
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _response;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) => _response = response;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_response(request));
    }
}
