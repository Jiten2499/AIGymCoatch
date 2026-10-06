using LifeFitAI.Api.Contracts;
using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Fitness.Nutrition;
using LifeFitAI.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeFitAI.Api.Controllers;

[Authorize]
public sealed class NutritionController : ApiControllerBase
{
    public NutritionController(MediatR.ISender sender) : base(sender)
    {
    }

    [HttpGet("nutrition/profile")]
    [RequiresPermission(PermissionCodes.NutritionRead)]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken) =>
        (await Sender.Send(new GetNutritionProfileQuery(), cancellationToken)).ToActionResult();

    [HttpPut("nutrition/profile")]
    [RequiresPermission(PermissionCodes.NutritionManage)]
    public async Task<IActionResult> SaveProfile(SaveNutritionProfileRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new SaveNutritionProfileCommand(request.DailyCalorieTarget, request.ProteinTarget, request.CarbTarget, request.FatTarget, request.DietaryPreference, request.MealFrequency), cancellationToken)).ToActionResult();

    [HttpGet("nutrition/logs")]
    [RequiresPermission(PermissionCodes.NutritionRead)]
    public async Task<IActionResult> Logs(DateOnly? date, int? page, int? pageSize, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListNutritionLogsQuery(date, page ?? 1, pageSize ?? 20), cancellationToken)).ToActionResult();

    [HttpPost("nutrition/logs")]
    [RequiresPermission(PermissionCodes.NutritionManage)]
    public async Task<IActionResult> CreateLog(CreateNutritionLogRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new CreateNutritionLogCommand(request.LogDate, request.MealType, request.FoodName, request.Quantity, request.Unit, request.Calories, request.Protein, request.Carbohydrates, request.Fat), cancellationToken)).ToActionResult();

    [HttpGet("nutrition/summary")]
    [RequiresPermission(PermissionCodes.NutritionRead)]
    public async Task<IActionResult> Summary(DateOnly date, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetNutritionSummaryQuery(date), cancellationToken)).ToActionResult();
}
