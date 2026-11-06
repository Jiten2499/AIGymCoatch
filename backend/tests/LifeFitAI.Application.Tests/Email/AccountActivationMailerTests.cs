using FluentAssertions;
using LifeFitAI.Application.Email;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Email;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Email;

public sealed class AccountActivationMailerTests
{
    [Fact]
    public async Task New_staff_welcome_queues_an_activation_link_without_a_password()
    {
        var email = new CapturingEmail();
        var resets = new MemoryResets();
        var mailer = Mailer(email, resets);
        await mailer.SendAsync(Notice(existing: false, RoleNames.Receptionist), CancellationToken.None);

        email.Requests.Should().ContainSingle();
        email.Requests[0].TemplateKey.Should().Be(EmailTemplateKeys.WelcomeStaff);
        email.Requests[0].HtmlBody.Should().Contain("Activate Account");
        email.Requests[0].TextBody.Should().NotContain("password:");
        email.Requests[0].IdempotencyKey.Should().StartWith("Welcome:");
        resets.Added.Should().ContainSingle();
    }

    [Fact]
    public async Task Existing_user_receives_an_invitation_without_a_reset_token()
    {
        var email = new CapturingEmail();
        var resets = new MemoryResets();
        var mailer = Mailer(email, resets);
        await mailer.SendAsync(Notice(existing: true, RoleNames.Trainer), CancellationToken.None);

        email.Requests.Should().ContainSingle();
        email.Requests[0].TemplateKey.Should().Be(EmailTemplateKeys.InvitationTrainer);
        email.Requests[0].HtmlBody.Should().NotContain("Activate Account");
        resets.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task Repeated_welcome_uses_the_same_idempotency_key()
    {
        var email = new CapturingEmail();
        var mailer = Mailer(email, new MemoryResets());
        var notice = Notice(existing: false, RoleNames.Member);
        await mailer.SendAsync(notice, CancellationToken.None);
        await mailer.SendAsync(notice, CancellationToken.None);

        email.Requests.Should().HaveCount(2);
        email.Requests[0].IdempotencyKey.Should().Be(email.Requests[1].IdempotencyKey);
    }

    [Fact]
    public async Task Queue_failure_does_not_throw()
    {
        var email = new CapturingEmail { Failure = EmailErrors.Disabled };
        var mailer = Mailer(email, new MemoryResets());
        var act = () => mailer.SendAsync(Notice(existing: false, RoleNames.Receptionist), CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Preview_rejects_an_unknown_token()
    {
        var handler = new GetActivationPreviewQueryHandler(new MemoryResets(), new HashTokens(), Substitute.For<IUserAccountRepository>());
        var result = await handler.Handle(new GetActivationPreviewQuery("missing"), CancellationToken.None);
        result.Error.Code.Should().Be(IdentityErrors.ResetInvalid.Code);
    }

    private static AccountActivationMailer Mailer(CapturingEmail email, MemoryResets resets)
    {
        var configuration = Substitute.For<IEmailConfigCache>();
        configuration.GetActiveAsync(Arg.Any<CancellationToken>()).Returns((EmailProviderConfiguration?)null);
        var users = Substitute.For<IUserAccountRepository>();
        users.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(UserAccount.Create(Guid.NewGuid(), Guid.NewGuid(), "person@gym.test", null, "hash", "salt", "Ada", "Lovelace"));
        var unit = Substitute.For<IUnitOfWork>();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc));
        return new AccountActivationMailer(email, new EmailTemplateRenderer(), configuration, resets, new HashTokens(), users, new FixedLinks(), clock, unit, NullLogger<AccountActivationMailer>.Instance);
    }

    private static AccountActivationNotice Notice(bool existing, string role) =>
        new(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "person@gym.test", "Ada", "Lovelace", role, Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "North Gym", existing);

    private sealed class CapturingEmail : IEmailService
    {
        public List<EmailMessageRequest> Requests { get; } = [];

        public Error? Failure { get; init; }

        public Task<Result<Guid>> QueueAsync(EmailMessageRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(Failure is null ? Result.Success(Guid.NewGuid()) : Result.Failure<Guid>(Failure));
        }
    }

    private sealed class MemoryResets : IPasswordResetRepository
    {
        public List<PasswordResetToken> Added { get; } = [];

        public Task AddAsync(PasswordResetToken token, CancellationToken cancellationToken)
        {
            Added.Add(token);
            return Task.CompletedTask;
        }

        public Task<PasswordResetToken?> GetActiveByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
            Task.FromResult(Added.SingleOrDefault(token => token.TokenHash == tokenHash && token.IsActive(DateTime.UtcNow)));

        public Task RevokeUnusedAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class HashTokens : ISecretTokenFactory
    {
        public string Create() => "raw-token";

        public string Hash(string token) => "hash:" + token;
    }

    private sealed class FixedLinks : IPublicAppLinks
    {
        public string LoginUrl => "http://localhost:3000/login";

        public string ActivationUrl(string token) => "http://localhost:3000/activate?token=" + token;

        public string MobileActivationUrl(string token) => "lifefit://activate?token=" + token;
    }
}
