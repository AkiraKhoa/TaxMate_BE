using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;

namespace TaxMate.Repository.Repositories;

public class TaxCalculationRepository : GenericRepository<TaxCalculation>, ITaxCalculationRepository
{
    public TaxCalculationRepository(DbContext context) : base(context)
    {
    }
}