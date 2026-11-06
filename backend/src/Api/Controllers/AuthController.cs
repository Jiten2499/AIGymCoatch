using LifeFitAI.Api.Contracts;
using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Email;
using LifeFitAI.Application.Identity.Login;
using LifeFitAI.Application.Identity.Password;
using LifeFitAI.Application.Identity.Refresh;
using LifeFitAI.Application.Identity.Register;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LifeFitAI.Api.Controllers;

public sealed class AuthController : ApiControllerBase
{
    public AuthController(MediatR.ISender sender) : base(sender)
    {
    }

    [HttpPost("auth/register")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new RegisterCommand(request.Email, request.Password, request.FirstName, request.LastName, request.Phone), cancellationToken)).ToActionResult();

    [HttpPost("auth/login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new LoginCommand(request.Email, request.Password), cancellationToken)).ToActionResult();

    [HttpPost("auth/refresh")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Refresh(RefreshRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new RefreshCommand(request.RefreshToken), cancellationToken)).ToActionResult();

    [HttpPost("auth/logout")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new LogoutCommand(request.RefreshToken), cancellationToken)).ToActionResult();

    [HttpPost("auth/forgot-password")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new ForgotPasswordCommand(request.Email), cancellationToken)).ToActionResult();

    [HttpPost("auth/activation/preview")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> PreviewActivation(ActivationPreviewRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetActivationPreviewQuery(request.Token), cancellationToken)).ToActionResult();

    [HttpPost("auth/reset-password")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new ResetPasswordCommand(request.Token, request.NewPassword), cancellationToken)).ToActionResult();

    [HttpPost("auth/change-password")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new ChangePasswordCommand(request.CurrentPassword, request.NewPassword), cancellationToken)).ToActionResult();
}
