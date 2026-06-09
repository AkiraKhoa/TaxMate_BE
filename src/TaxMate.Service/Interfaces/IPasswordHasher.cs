namespace TaxMate.Service.Interfaces;

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);
}
