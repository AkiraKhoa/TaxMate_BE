using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TaxMate.Service.Interfaces;

public class CassoTokenResponse
{
    public string AccessToken { get; set; } = null!;
    public string RefreshToken { get; set; } = null!;
    public string TokenType { get; set; } = null!;
    public int ExpiresIn { get; set; }
}

public class CassoAccountDto
{
    public int Id { get; set; }
    public string BankName { get; set; } = null!;
    public string BankAccountName { get; set; } = null!;
    public string BankAccountNumber { get; set; } = null!;
}

public interface ICassoService
{
    string GetAuthorizationUrl(Guid businessId, string redirectUri);
    Task<CassoTokenResponse> ExchangeCodeForTokensAsync(string code, string redirectUri);
    Task<IEnumerable<CassoAccountDto>> GetBankAccountsAsync(string accessToken);
}
