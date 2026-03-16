using AutoMapper;
using Microsoft.AspNetCore.Http;
using System.Reflection;

namespace Lascodia.Trading.Engine.SharedLibrary.Mappings;

public class MappingProfile : Profile
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    public MappingProfile(IHttpContextAccessor httpContextAccessor, Assembly assembly)
    {
        _httpContextAccessor = httpContextAccessor;
        ApplyMappingsFromAssembly(assembly);
    }

    private void ApplyMappingsFromAssembly(Assembly assembly)
    {
        var types = assembly.GetExportedTypes()
            .Where(t => t.GetInterfaces().Any(i => 
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMapFrom<>)))
            .ToList();

        foreach (var type in types)
        {
            var instance = Activator.CreateInstance(type);

            var methodInfo = type.GetMethod("Mapping") 
                ?? type.GetInterface("IMapFrom`1")?.GetMethod("Mapping");
            
            methodInfo?.Invoke(instance, new object[] { this, _httpContextAccessor });
        }
    }
}
