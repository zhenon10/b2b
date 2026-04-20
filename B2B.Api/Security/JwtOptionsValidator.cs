using Microsoft.Extensions.Options;

namespace B2B.Api.Security;

/// <summary>
/// Ensures JWT options are never started with missing or weak configuration.
/// Secrets must come from environment variables, Azure Key Vault, or User Secrets (Development).
/// </summary>
public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Issuer))
            return ValidateOptionsResult.Fail("Jwt:Issuer is required.");

        if (string.IsNullOrWhiteSpace(options.Audience))
            return ValidateOptionsResult.Fail("Jwt:Audience is required.");

        var signingKey = !string.IsNullOrWhiteSpace(options.SigningKey)
            ? options.SigningKey
            : options.Key;

        if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32)
        {
            return ValidateOptionsResult.Fail(
                "JWT signing key is missing or shorter than 32 characters. " +
                "Set Jwt:SigningKey (or Jwt:Key) via environment variable Jwt__SigningKey, a secret store, " +
                "or for local development: dotnet user-secrets set \"Jwt:SigningKey\" \"<your-key>\" --project B2B.Api");
        }

        if (options.RefreshTokenDays < 1 || options.RefreshTokenDays > 366)
        {
            return ValidateOptionsResult.Fail("Jwt:RefreshTokenDays must be between 1 and 366.");
        }

        return ValidateOptionsResult.Success;
    }
}
