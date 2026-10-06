using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Ai;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeFitAI.Api.Controllers;

[Authorize]
public sealed class LlmConnectionsController : ApiControllerBase
{
    public LlmConnectionsController(MediatR.ISender sender) : base(sender)
    {
    }

    [HttpGet("llm/connections")]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        (await Sender.Send(new ListLlmConnectionsQuery(), cancellationToken)).ToActionResult();

    [HttpPost("llm/connections")]
    public async Task<IActionResult> Create([FromBody] CreateLlmConnectionBody body, CancellationToken cancellationToken) =>
        (await Sender.Send(new CreateLlmConnectionCommand(body.Name, body.Provider, body.BaseUrl, body.ApiKey, body.DefaultModel, body.IsActive, body.IsDefault, body.TimeoutSeconds), cancellationToken)).ToActionResult();

    [HttpPut("llm/connections/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLlmConnectionBody body, CancellationToken cancellationToken) =>
        (await Sender.Send(new UpdateLlmConnectionCommand(id, body.Name, body.BaseUrl, body.ApiKey, body.DefaultModel, body.IsActive, body.IsDefault, body.TimeoutSeconds), cancellationToken)).ToActionResult();

    [HttpPost("llm/connections/{id:guid}/default")]
    public async Task<IActionResult> Default(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new SetDefaultLlmConnectionCommand(id), cancellationToken)).ToActionResult();

    [HttpPost("llm/connections/{id:guid}/test")]
    public async Task<IActionResult> Test(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new TestLlmConnectionCommand(id), cancellationToken)).ToActionResult();

    [HttpDelete("llm/connections/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new DeleteLlmConnectionCommand(id), cancellationToken)).ToActionResult();

    [HttpGet("llm/connections/{id:guid}/models")]
    public async Task<IActionResult> Models(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListLlmModelsQuery(id), cancellationToken)).ToActionResult();

    [HttpPost("llm/connections/{id:guid}/models")]
    public async Task<IActionResult> CreateModel(Guid id, [FromBody] CreateLlmModelBody body, CancellationToken cancellationToken) =>
        (await Sender.Send(new CreateLlmModelCommand(id, body.ModelIdentifier, body.DisplayName, body.IsActive, body.IsDefault), cancellationToken)).ToActionResult();

    [HttpPost("llm/connections/{id:guid}/models/refresh")]
    public async Task<IActionResult> Refresh(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new RefreshLlmModelsCommand(id), cancellationToken)).ToActionResult();

    [HttpPut("llm/models/{id:guid}")]
    public async Task<IActionResult> UpdateModel(Guid id, [FromBody] UpdateLlmModelBody body, CancellationToken cancellationToken) =>
        (await Sender.Send(new UpdateLlmModelCommand(id, body.DisplayName, body.IsActive, body.IsDefault), cancellationToken)).ToActionResult();

    [HttpDelete("llm/models/{id:guid}")]
    public async Task<IActionResult> DeleteModel(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new DeleteLlmModelCommand(id), cancellationToken)).ToActionResult();
}

public sealed record CreateLlmConnectionBody(string Name, string Provider, string BaseUrl, string ApiKey, string? DefaultModel, bool IsActive, bool IsDefault, int TimeoutSeconds);

public sealed record UpdateLlmConnectionBody(string Name, string BaseUrl, string? ApiKey, string? DefaultModel, bool IsActive, bool IsDefault, int TimeoutSeconds);

public sealed record CreateLlmModelBody(string ModelIdentifier, string DisplayName, bool IsActive, bool IsDefault);

public sealed record UpdateLlmModelBody(string DisplayName, bool IsActive, bool IsDefault);
