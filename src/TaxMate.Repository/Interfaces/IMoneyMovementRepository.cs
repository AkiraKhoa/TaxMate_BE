using TaxMate.Model.DTO.MoneyMovement;
using TaxMate.Model.Entities;

namespace TaxMate.Repository.Interfaces;

public interface IMoneyMovementRepository
{
    Task<Guid?> GetBusinessOwnerIdAsync(
        Guid businessId,
        CancellationToken cancellationToken = default);

    Task<PaymentAccount?> GetAccountForWriteAsync(
        Guid paymentAccountId,
        CancellationToken cancellationToken = default);

    Task<MoneyMovement?> GetBySourceForWriteAsync(
        string movementType,
        Guid referenceId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        MoneyMovement movement,
        CancellationToken cancellationToken = default);

    void Remove(MoneyMovement movement);

    Task<IReadOnlyList<PaymentAccount>> GetAccountsForBusinessAsync(
        Guid businessId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MoneyMovement>> GetMovementsForBusinessBeforeAsync(
        Guid businessId,
        DateTime toExclusive,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MoneyMovementSourceAuditRecord>> GetExpectedSourcesBeforeAsync(
        Guid businessId,
        DateTime toExclusive,
        CancellationToken cancellationToken = default);

    Task<IReadOnlySet<Guid>> GetSystemIncomeIdsBeforeAsync(
        Guid businessId,
        DateTime toExclusive,
        CancellationToken cancellationToken = default);
}
