namespace Contacts.Application.Interfaces;

/// <summary>
/// JWT token service contract. Infrastructure provides the concrete implementation.
/// </summary>
public interface IJwtTokenService
{
    string GenerateToken(string userId, string email, string[] roles);
}
