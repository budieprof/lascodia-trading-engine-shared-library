using AutoMapper;
using Microsoft.AspNetCore.Http;

namespace Lascodia.Trading.Engine.SharedLibrary.Mappings;

public interface IMapFrom<T>
{   
    public void Mapping(Profile profile, IHttpContextAccessor httpContextAssessor) => profile.CreateMap(typeof(T), GetType());
}
