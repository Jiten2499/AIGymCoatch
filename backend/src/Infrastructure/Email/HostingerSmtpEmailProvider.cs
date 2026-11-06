using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using LifeFitAI.Application.Email;
using LifeFitAI.Domain.Email;
using Microsoft.Extensions.Logging;

namespace LifeFitAI.Infrastructure.Email;

public sealed class HostingerSmtpEmailProvider : IEmailProvider
{
    private readonly ILogger<HostingerSmtpEmailProvider> _logger;

    public HostingerSmtpEmailProvider(ILogger<HostingerSmtpEmailProvider> logger) => _logger = logger;

    public string ProviderKey => EmailProviders.SmtpHostinger;

    public async Task<EmailTestResponse> TestConnectionAsync(EmailProviderSettings settings, CancellationToken cancellationToken)
    {
        var testedAt = DateTime.UtcNow;
        try
        {
            await using var session = await SmtpSession.ConnectAsync(settings, cancellationToken).ConfigureAwait(false);
            await session.AuthenticateAsync(cancellationToken).ConfigureAwait(false);
            await session.QuitAsync(cancellationToken).ConfigureAwait(false);
            return new EmailTestResponse(true, "Healthy", "SMTP connection succeeded.", testedAt);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "SMTP connection test failed for {Host}:{Port}", settings.SmtpHost, settings.SmtpPort);
            return new EmailTestResponse(false, "Failed", Sanitize(exception.Message), testedAt);
        }
    }

    public async Task<EmailSendResult> SendAsync(EmailProviderSettings settings, EmailMessageRequest message, CancellationToken cancellationToken)
    {
        try
        {
            await using var session = await SmtpSession.ConnectAsync(settings, cancellationToken).ConfigureAwait(false);
            await session.AuthenticateAsync(cancellationToken).ConfigureAwait(false);
            await session.WriteAsync($"MAIL FROM:<{settings.SenderEmail}>\r\n", cancellationToken).ConfigureAwait(false);
            var mailFrom = await session.ReadReplyAsync(cancellationToken).ConfigureAwait(false);
            if (!mailFrom.StartsWith("250", StringComparison.Ordinal))
            {
                return new EmailSendResult(false, null, Sanitize($"MAIL FROM rejected: {mailFrom}"), false);
            }

            await session.WriteAsync($"RCPT TO:<{message.Recipient}>\r\n", cancellationToken).ConfigureAwait(false);
            var rcpt = await session.ReadReplyAsync(cancellationToken).ConfigureAwait(false);
            if (!rcpt.StartsWith("250", StringComparison.Ordinal) && !rcpt.StartsWith("251", StringComparison.Ordinal))
            {
                return new EmailSendResult(false, null, Sanitize($"RCPT TO rejected: {rcpt}"), false);
            }

            await session.WriteAsync("DATA\r\n", cancellationToken).ConfigureAwait(false);
            _ = await session.ReadReplyAsync(cancellationToken).ConfigureAwait(false);
            var boundary = "lifefit-" + Guid.NewGuid().ToString("N");
            var body = new StringBuilder();
            body.Append("From: ").Append(EncodeHeader(settings.SenderName)).Append(" <").Append(settings.SenderEmail).Append(">\r\n");
            body.Append("To: <").Append(message.Recipient).Append(">\r\n");
            if (!string.IsNullOrWhiteSpace(settings.ReplyToEmail))
            {
                body.Append("Reply-To: <").Append(settings.ReplyToEmail).Append(">\r\n");
            }

            body.Append("Subject: ").Append(EncodeHeader(message.Subject)).Append("\r\n");
            body.Append("MIME-Version: 1.0\r\n");
            body.Append("Content-Type: multipart/alternative; boundary=\"").Append(boundary).Append("\"\r\n\r\n");
            body.Append("--").Append(boundary).Append("\r\nContent-Type: text/plain; charset=utf-8\r\n\r\n").Append(message.TextBody).Append("\r\n");
            body.Append("--").Append(boundary).Append("\r\nContent-Type: text/html; charset=utf-8\r\n\r\n").Append(message.HtmlBody).Append("\r\n");
            body.Append("--").Append(boundary).Append("--\r\n.\r\n");
            await session.WriteAsync(body.ToString(), cancellationToken).ConfigureAwait(false);
            var data = await session.ReadReplyAsync(cancellationToken).ConfigureAwait(false);
            await session.QuitAsync(cancellationToken).ConfigureAwait(false);
            if (!data.StartsWith("250", StringComparison.Ordinal))
            {
                return new EmailSendResult(false, null, Sanitize($"DATA rejected: {data}"), IsTransient(data));
            }

            return new EmailSendResult(true, ExtractMessageId(data) ?? Guid.NewGuid().ToString("N"), null, false);
        }
        catch (Exception exception) when (exception is OperationCanceledException or IOException or SocketException)
        {
            _logger.LogWarning(exception, "Transient SMTP send failure for template {Template}", message.TemplateKey);
            return new EmailSendResult(false, null, Sanitize(exception.Message), true);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "SMTP send failure for template {Template}", message.TemplateKey);
            return new EmailSendResult(false, null, Sanitize(exception.Message), false);
        }
    }

    private static string EncodeHeader(string value)
    {
        var cleaned = value.Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal);
        if (cleaned.All(character => character < 128))
        {
            return cleaned;
        }

        return "=?UTF-8?B?" + Convert.ToBase64String(Encoding.UTF8.GetBytes(cleaned)) + "?=";
    }

    private static string? ExtractMessageId(string reply)
    {
        var start = reply.IndexOf('<');
        var end = reply.IndexOf('>');
        return start >= 0 && end > start ? reply[(start + 1)..end] : null;
    }

    private static bool IsTransient(string reply) =>
        reply.StartsWith("421", StringComparison.Ordinal)
        || reply.StartsWith("450", StringComparison.Ordinal)
        || reply.StartsWith("451", StringComparison.Ordinal)
        || reply.StartsWith("452", StringComparison.Ordinal);

    private static string Sanitize(string message)
    {
        var cleaned = message
            .Replace("password", "****", StringComparison.OrdinalIgnoreCase)
            .Replace("auth login", "auth ****", StringComparison.OrdinalIgnoreCase);
        return cleaned.Length <= 400 ? cleaned : cleaned[..400];
    }

    private sealed class SmtpSession : IAsyncDisposable
    {
        private readonly TcpClient _client;
        private readonly Stream _stream;
        private readonly EmailProviderSettings _settings;

        private SmtpSession(TcpClient client, Stream stream, EmailProviderSettings settings)
        {
            _client = client;
            _stream = stream;
            _settings = settings;
        }

        public static async Task<SmtpSession> ConnectAsync(EmailProviderSettings settings, CancellationToken cancellationToken)
        {
            var client = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(settings.ConnectionTimeoutSeconds, 1, 120)));
            await client.ConnectAsync(settings.SmtpHost, settings.SmtpPort, timeout.Token).ConfigureAwait(false);
            Stream stream = client.GetStream();
            if (settings.Encryption == EmailEncryption.SslTls)
            {
                var ssl = new SslStream(stream, leaveInnerStreamOpen: false);
                await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = settings.SmtpHost }, timeout.Token).ConfigureAwait(false);
                stream = ssl;
                var session = new SmtpSession(client, stream, settings);
                var greeting = await session.ReadReplyAsync(timeout.Token).ConfigureAwait(false);
                if (!greeting.StartsWith("220", StringComparison.Ordinal))
                {
                    await session.DisposeAsync().ConfigureAwait(false);
                    throw new InvalidOperationException("Unexpected SMTP greeting.");
                }

                await session.WriteAsync($"EHLO {EhloHost(settings)}\r\n", timeout.Token).ConfigureAwait(false);
                _ = await session.ReadReplyAsync(timeout.Token).ConfigureAwait(false);
                return session;
            }

            var plain = new SmtpSession(client, stream, settings);
            var plainGreeting = await plain.ReadReplyAsync(timeout.Token).ConfigureAwait(false);
            if (!plainGreeting.StartsWith("220", StringComparison.Ordinal))
            {
                await plain.DisposeAsync().ConfigureAwait(false);
                throw new InvalidOperationException("Unexpected SMTP greeting.");
            }

            await plain.WriteAsync($"EHLO {EhloHost(settings)}\r\n", timeout.Token).ConfigureAwait(false);
            _ = await plain.ReadReplyAsync(timeout.Token).ConfigureAwait(false);
            await plain.WriteAsync("STARTTLS\r\n", timeout.Token).ConfigureAwait(false);
            var startTls = await plain.ReadReplyAsync(timeout.Token).ConfigureAwait(false);
            if (!startTls.StartsWith("220", StringComparison.Ordinal))
            {
                await plain.DisposeAsync().ConfigureAwait(false);
                throw new InvalidOperationException("STARTTLS was rejected.");
            }

            var upgraded = new SslStream(stream, leaveInnerStreamOpen: false);
            await upgraded.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = settings.SmtpHost }, timeout.Token).ConfigureAwait(false);
            var secure = new SmtpSession(client, upgraded, settings);
            await secure.WriteAsync($"EHLO {EhloHost(settings)}\r\n", timeout.Token).ConfigureAwait(false);
            _ = await secure.ReadReplyAsync(timeout.Token).ConfigureAwait(false);
            return secure;
        }

        public async Task AuthenticateAsync(CancellationToken cancellationToken)
        {
            if (!_settings.AuthenticationEnabled)
            {
                return;
            }

            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"\0{_settings.Username}\0{_settings.Password}"));
            await WriteAsync($"AUTH PLAIN {token}\r\n", cancellationToken).ConfigureAwait(false);
            var plain = await ReadReplyAsync(cancellationToken).ConfigureAwait(false);
            if (plain.StartsWith("235", StringComparison.Ordinal))
            {
                return;
            }

            if (plain.StartsWith("535", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(AuthFailure(plain));
            }

            if (!plain.StartsWith("504", StringComparison.Ordinal) && !plain.StartsWith("503", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(AuthFailure(plain));
            }

            await WriteAsync("AUTH LOGIN\r\n", cancellationToken).ConfigureAwait(false);
            var challenge = await ReadReplyAsync(cancellationToken).ConfigureAwait(false);
            if (!challenge.StartsWith("334", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(AuthFailure(string.IsNullOrWhiteSpace(challenge) ? plain : challenge));
            }

            await WriteAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes(_settings.Username)) + "\r\n", cancellationToken).ConfigureAwait(false);
            var userReply = await ReadReplyAsync(cancellationToken).ConfigureAwait(false);
            if (!userReply.StartsWith("334", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(AuthFailure(userReply));
            }

            await WriteAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes(_settings.Password ?? string.Empty)) + "\r\n", cancellationToken).ConfigureAwait(false);
            var login = await ReadReplyAsync(cancellationToken).ConfigureAwait(false);
            if (!login.StartsWith("235", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(AuthFailure(login));
            }
        }

        private static string EhloHost(EmailProviderSettings settings)
        {
            var source = string.IsNullOrWhiteSpace(settings.SenderEmail) ? settings.Username : settings.SenderEmail;
            var at = source.LastIndexOf('@');
            var domain = at > 0 && at < source.Length - 1 ? source[(at + 1)..].Trim() : "localhost";
            return domain.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-') ? domain : "localhost";
        }

        private static string AuthFailure(string reply)
        {
            var first = reply.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "authentication failed";
            if (first.StartsWith("535", StringComparison.Ordinal))
            {
                return "Hostinger rejected these SMTP credentials. Replace the saved mailbox secret for this username, then test again.";
            }

            return $"SMTP authentication failed ({first}).";
        }

        public Task QuitAsync(CancellationToken cancellationToken) => WriteAsync("QUIT\r\n", cancellationToken);

        public async Task WriteAsync(string value, CancellationToken cancellationToken)
        {
            var bytes = Encoding.ASCII.GetBytes(value);
            await _stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<string> ReadReplyAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];
            var builder = new StringBuilder();
            while (true)
            {
                var read = await _stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                if (read <= 0)
                {
                    break;
                }

                builder.Append(Encoding.ASCII.GetString(buffer, 0, read));
                var text = builder.ToString();
                if (text.Contains('\n') && !HasMoreLines(text))
                {
                    return text.Trim();
                }

                if (builder.Length > 16_384)
                {
                    break;
                }
            }

            return builder.ToString().Trim();
        }

        public async ValueTask DisposeAsync()
        {
            await _stream.DisposeAsync().ConfigureAwait(false);
            _client.Dispose();
        }

        private static bool HasMoreLines(string text)
        {
            var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0)
            {
                return true;
            }

            var last = lines[^1];
            return last.Length >= 4 && last[3] == '-';
        }
    }
}
