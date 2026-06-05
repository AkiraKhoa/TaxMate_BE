using TaxMate.Model.Entities;

namespace TaxMate.Service.Interfaces;

public interface IJwtService
{
    (string Token, DateTime ExpiresAt) GenerateToken(User user);
}
