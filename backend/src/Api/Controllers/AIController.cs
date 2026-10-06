using LifeFitAI.Api.Contracts;
using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Fitness.Ai;
using LifeFitAI.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LifeFitAI.Api.Controllers;

[Authorize]
[EnableRateLimiting("ai")]
public sealed class AIController : ApiControllerBase
{
    public AIController(MediatR.ISender sender) : base(sender)
    {
    }

    [HttpPost("ai/coach/chat")]
    [RequiresPermission(PermissionCodes.AiUse)]
    public async Task<IActionResult> Coach(CoachChatRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new CoachChatCommand(request.Message, request.ConversationId), cancellationToken)).ToActionResult();

    [HttpPost("ai/workouts/generate")]
    [RequiresPermission(PermissionCodes.AiUse)]
    public async Task<IActionResult> GenerateWorkout(GenerateWorkoutRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new GenerateWorkoutCommand(request.Notes), cancellationToken)).ToActionResult();

    [HttpPost("ai/nutrition/advice")]
    [RequiresPermission(PermissionCodes.AiUse)]
    public async Task<IActionResult> NutritionAdvice(NutritionAdviceRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new NutritionAdviceCommand(request.Question), cancellationToken)).ToActionResult();

    [HttpPost("ai/progress/insight")]
    [RequiresPermission(PermissionCodes.AiUse)]
    public async Task<IActionResult> ProgressInsight(CancellationToken cancellationToken) =>
        (await Sender.Send(new ProgressInsightCommand(), cancellationToken)).ToActionResult();

    [HttpGet("ai/conversations")]
    [RequiresPermission(PermissionCodes.AiUse)]
    public async Task<IActionResult> Conversations(int? page, int? pageSize, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListConversationsQuery(page ?? 1, pageSize ?? 20), cancellationToken)).ToActionResult();

    [HttpGet("ai/conversations/{id:guid}")]
    [RequiresPermission(PermissionCodes.AiUse)]
    public async Task<IActionResult> Conversation(Guid id, int? page, int? pageSize, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetConversationQuery(id, page ?? 1, pageSize ?? 20), cancellationToken)).ToActionResult();
}
