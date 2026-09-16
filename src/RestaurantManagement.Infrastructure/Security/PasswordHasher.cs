using System.Security.Cryptography;
using RestaurantManagement.Application.Common.Interfaces;

namespace RestaurantManagement.Infrastructure.Security;

/// <summary>
/// Standard PBKDF2 adaptive password hasher using HMAC-SHA256 and cryptographic salts.
/// </summary>
public class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16; // 128 bit
    private const int KeySize = 32;  // 256 bit
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;
    private const char SegmentDelimiter = '$';

    public string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password cannot be null or empty.", nameof(password));

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            Algorithm,
            KeySize);

        return string.Join(
            SegmentDelimiter,
            "pbkdf2",
            Iterations,
            Convert.ToHexString(salt),
            Convert.ToHexString(hash));
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(passwordHash))
            return false;

        var segments = passwordHash.Split(SegmentDelimiter);
        if (segments.Length != 4 || segments[0] != "pbkdf2")
            return false;

        if (!int.TryParse(segments[1], out var iterations))
            return false;

        try
        {
            var salt = Convert.FromHexString(segments[2]);
            var expectedHash = Convert.FromHexString(segments[3]);

            var actualHash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                Algorithm,
                expectedHash.Length);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch
        {
            return false;
        }
    }
}
