// =============================================================================
// SecureHasher.cs — Resolve Hub Security Layer
// Implements PBKDF2/HMAC-SHA256 password hashing with a cryptographic salt.
// Passwords are NEVER stored in plain text anywhere in this application.
// =============================================================================

using System;
using System.Security.Cryptography;

namespace ResolveHub.Core.Security
{
    /// <summary>
    /// Provides PBKDF2-based password hashing and verification.
    /// Decision: PBKDF2 with HMAC-SHA256 at 100,000 iterations was chosen over
    /// BCrypt because it is built into .NET's standard library, requires no
    /// external dependency, and meets NIST SP 800-132 recommendations.
    /// </summary>
    public static class SecureHasher
    {
        // -----------------------------------------------------------------
        // Constants — centralised so changing them is a one-line operation
        // -----------------------------------------------------------------
        private const int SaltBytes       = 16;        // 128-bit random salt
        private const int HashBytes       = 32;        // 256-bit derived key
        private const int Iterations      = 100_000;   // NIST-recommended minimum
        private const char Delimiter      = ':';       // separator in stored string

        // -----------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------

        /// <summary>
        /// Hashes a plain-text password into a storable string.
        /// Format: base64(salt):base64(hash)
        /// </summary>
        /// <param name="plainPassword">The user's plain-text password.</param>
        /// <returns>A storable hash string.</returns>
        /// <exception cref="ArgumentException">Thrown when password is null or empty.</exception>
        public static string Hash(string plainPassword)
        {
            if (string.IsNullOrWhiteSpace(plainPassword))
                throw new ArgumentException("Password cannot be null or empty.", nameof(plainPassword));

            // Generate a cryptographically secure random salt
            byte[] salt = RandomNumberGenerator.GetBytes(SaltBytes);

            // Derive the key using PBKDF2 / HMAC-SHA256
            byte[] hash = DeriveKey(plainPassword, salt);

            // Return "base64Salt:base64Hash" — safe to store in SQLite TEXT column
            return $"{Convert.ToBase64String(salt)}{Delimiter}{Convert.ToBase64String(hash)}";
        }

        /// <summary>
        /// Verifies a plain-text password against a previously hashed string.
        /// Uses constant-time comparison to prevent timing attacks.
        /// </summary>
        /// <param name="plainPassword">The password to verify.</param>
        /// <param name="storedHash">The hash string previously produced by <see cref="Hash"/>.</param>
        /// <returns>True if the password matches; otherwise false.</returns>
        public static bool Verify(string plainPassword, string storedHash)
        {
            if (string.IsNullOrWhiteSpace(plainPassword) || string.IsNullOrWhiteSpace(storedHash))
                return false;

            try
            {
                // Split stored value back into salt + hash
                string[] parts = storedHash.Split(Delimiter);
                if (parts.Length != 2)
                    return false;

                byte[] salt         = Convert.FromBase64String(parts[0]);
                byte[] expectedHash = Convert.FromBase64String(parts[1]);

                // Re-derive with the same salt
                byte[] actualHash = DeriveKey(plainPassword, salt);

                // Constant-time comparison — prevents timing-based brute force
                return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
            }
            catch
            {
                // Any parse failure is treated as a non-match — fail safe
                return false;
            }
        }

        // -----------------------------------------------------------------
        // Private helpers
        // -----------------------------------------------------------------

        /// <summary>
        /// Runs the PBKDF2 key derivation function with HMAC-SHA256.
        /// </summary>
        private static byte[] DeriveKey(string password, byte[] salt)
        {
            return Rfc2898DeriveBytes.Pbkdf2(
                password:       password,
                salt:           salt,
                iterations:     Iterations,
                hashAlgorithm:  HashAlgorithmName.SHA256,
                outputLength:   HashBytes);
        }
    }
}
