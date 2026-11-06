namespace LifeFitAI.Application.Email;

public static class AccountEmailLayout
{
    public static (string Subject, string HtmlBody, string TextBody) Render(string templateKey, IReadOnlyDictionary<string, string> values)
    {
        var name = Value(values, "name");
        var role = Value(values, "role");
        var business = Value(values, "business");
        var email = Value(values, "email");
        var loginUrl = Value(values, "loginUrl");
        var link = Value(values, "link");
        var mobileLink = Value(values, "mobileLink");
        var expires = Value(values, "expires");
        var support = Value(values, "support");
        var activation = templateKey.StartsWith("Account.Welcome", StringComparison.Ordinal);
        var audience = templateKey.Contains("Trainer", StringComparison.Ordinal)
            ? "trainer"
            : templateKey.Contains("Member", StringComparison.Ordinal) ? "member" : "staff";
        var subject = activation
            ? $"Welcome to LifeFit AI, {name}"
            : $"You're invited to {business}";
        var intro = activation
            ? $"Your {audience} account is ready. Activate it and choose your own password. LifeFit AI will never email a password."
            : $"An existing LifeFit AI account for {email} was linked to {business}. Sign in with your current password. A new password was not created.";
        var action = activation
            ? $"""<p style="margin:24px 0"><a href="{Encode(link)}" style="background:#173024;color:#f3f7f2;padding:14px 22px;border-radius:999px;text-decoration:none;font-weight:700">Activate Account &amp; Set Password</a></p><p>This link expires {Encode(expires)} and can be used once.</p><p>On a phone, you can open <a href="{Encode(mobileLink)}">the LifeFit AI app</a>. If the app is not installed, use the button above.</p>"""
            : $"""<p style="margin:24px 0"><a href="{Encode(loginUrl)}" style="background:#173024;color:#f3f7f2;padding:14px 22px;border-radius:999px;text-decoration:none;font-weight:700">Sign in to LifeFit AI</a></p>""";
        var html = $"""
            <div style="margin:0;padding:24px;background:#f4f7f4;font-family:Segoe UI,Arial,sans-serif;color:#173024">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:560px;margin:0 auto;background:#ffffff;border-radius:16px">
                <tr><td style="padding:28px">
                  <p style="margin:0;letter-spacing:.12em;font-size:12px;font-weight:700">LIFE FIT AI</p>
                  <h1 style="margin:12px 0;font-size:24px">{Encode(subject)}</h1>
                  <p>Hello {Encode(name)},</p>
                  <p>{Encode(intro)}</p>
                  <p><strong>Account type:</strong> {Encode(audience)}<br/>
                  <strong>Role:</strong> {Encode(role)}<br/>
                  <strong>Business:</strong> {Encode(business)}<br/>
                  <strong>Email:</strong> {Encode(email)}</p>
                  {action}
                  <p><a href="{Encode(loginUrl)}">Login</a></p>
                  <p style="color:#4d6356">{Encode(support)}</p>
                </td></tr>
              </table>
            </div>
            """;
        var text = activation
            ? $"{subject}\nHello {name},\n{intro}\nRole: {role}\nBusiness: {business}\nEmail: {email}\nActivate: {link}\nApp: {mobileLink}\nExpires: {expires}\nLogin: {loginUrl}\n{support}"
            : $"{subject}\nHello {name},\n{intro}\nRole: {role}\nBusiness: {business}\nEmail: {email}\nLogin: {loginUrl}\n{support}";
        return (subject, html, text);
    }

    private static string Value(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) ? value : "";

    private static string Encode(string value) => System.Net.WebUtility.HtmlEncode(value);
}
