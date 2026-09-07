using System.Security.Cryptography;

namespace Backend.Authorization;

public static class AuthenticationSession
{
    public const string ClaimType = "session_version";

    public static string CreateVersion()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    }
}
