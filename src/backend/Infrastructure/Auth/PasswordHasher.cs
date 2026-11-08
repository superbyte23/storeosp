using System.Text;

namespace StoreOps.Infrastructure.Auth;

public sealed class PasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        if (Encoding.UTF8.GetByteCount(password) > 72)
            throw new ArgumentException("BCrypt passwords cannot exceed 72 UTF-8 bytes.", nameof(password));

        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: WorkFactor);
    }

    public bool Verify(string password, string passwordHash)
    {
        if (string.IsNullOrEmpty(password) ||
            Encoding.UTF8.GetByteCount(password) > 72 ||
            string.IsNullOrEmpty(passwordHash))
            return false;

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }
}
