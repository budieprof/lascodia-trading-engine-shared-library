using Microsoft.EntityFrameworkCore;
using Lascodia.Trading.Engine.SharedDomain.Common;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Lascodia.Trading.Engine.SharedApplication.Common.Extension;

[ExcludeFromCodeCoverage]
public static class ModelBuilderExtensions
{
    public static void RegisterAllEntities(this ModelBuilder modelBuilder, params Assembly[] assemblies)
    {
        List<Type> types = assemblies.SelectMany(a => a.GetExportedTypes()).Where(c => c.IsClass && !c.IsAbstract && c.IsPublic && c.BaseType.IsGenericType && c.BaseType.GetGenericTypeDefinition() == typeof(Entity<>)).ToList();
        foreach (Type type in types)
            modelBuilder.Entity(type);
    }
}
