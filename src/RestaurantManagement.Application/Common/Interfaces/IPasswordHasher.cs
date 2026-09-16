namespace RestaurantManagement.Application.Common.Interfaces;

/// <summary>
/// Service contract for cryptographically hashing and verifying passwords.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Hashes a plaintext password using standard adaptive algorithm (PBKDF2).
    /// </summary>
    string HashPassword(string password);

    /// <summary>
    /// Verifies a plaintext password against a stored cryptographic hash.
    /// </summary>
    bool VerifyPassword(string password, string passwordHash);
}
