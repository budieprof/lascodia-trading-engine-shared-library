using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Lascodia.Trading.Engine.SharedInfrastructure.Persistence;

public abstract class BaseApplicationDbContext<T> : DbContext where T : DbContext
{
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly Assembly _assembly;
    public BaseApplicationDbContext(DbContextOptions<T> options, IHttpContextAccessor httpContextAccessor, Assembly assembly)
        : base(options)
    {
        this.httpContextAccessor = httpContextAccessor;
        _assembly = assembly;
    }


    public override int SaveChanges()
    {
        var result = base.SaveChanges();
        return result;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var result = await base.SaveChangesAsync(cancellationToken);
        return result;
    }



    protected override void OnModelCreating(ModelBuilder builder)
    {

        builder.ApplyConfigurationsFromAssembly(_assembly);

        base.OnModelCreating(builder);
    }
}
