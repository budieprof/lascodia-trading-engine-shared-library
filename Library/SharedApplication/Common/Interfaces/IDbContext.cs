using Microsoft.EntityFrameworkCore;

namespace Lascodia.Trading.Engine.SharedApplication.Common.Interfaces;

public interface IDbContext
{
    DbContext GetDbContext();
    int SaveChanges();
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
