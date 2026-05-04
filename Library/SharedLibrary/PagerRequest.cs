using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Http;
using Lascodia.Trading.Engine.SharedLibrary.Mappings;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Text.Json.Serialization;

namespace Lascodia.Trading.Engine.SharedLibrary;

[ExcludeFromCodeCoverage]
public class PagerRequest<TResponse> : IRequest<TResponse>, IMapFrom<Pager>
{
    [DefaultValue(5)]
    public int ItemCountPerPage { get; set; } = 5;
    private int _currentPage = 1;
    [DefaultValue(1)]
    public int CurrentPage
    {
        get
        {
            return _currentPage;
        }
        set
        {
            if (value < 1)
            {
                value = 1;
            }
            _currentPage = value;
        }

    }
    [JsonIgnore]
    public bool IncludeAllInList { get; set; } = true;
    [JsonIgnore]
    public int LimiterEndValue { get; set; } = 50;
    [JsonIgnore]
    public int LimiterSpacing { get; set; } = 1;
    [JsonIgnore]
    public int PageLink { get; set; } = 5;
    /// <summary>
    /// Server-side sort column. Maps to a DTO/entity property name via the
    /// query handler's safelist; values outside the safelist are ignored
    /// (the handler's default order is used). Required so sortable tables
    /// in the admin UI consult the database rather than only sorting the
    /// current in-memory page.
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    /// Sort direction — <c>"asc"</c> or <c>"desc"</c> (case-insensitive).
    /// Defaults to <c>"desc"</c> when only <see cref="SortBy"/> is set.
    /// </summary>
    public string? SortDirection { get; set; }

    [DefaultValue("{}")]
    public string? Filter { get; set; } = "{}";
    public dynamic? FilterObj
    {
        get
        {
            if (this.Filter == null) return null;
            return Newtonsoft.Json.JsonConvert.DeserializeObject<ExpandoObject>(Filter);
        }
    }

    public T? GetFilter<T>()
    {
        if (this.Filter == null) return default(T);
        return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(Filter);
    }

    public void Mapping(Profile profile, IHttpContextAccessor httpContextAssessor)
    {
        profile.CreateMap(GetType(), typeof(Pager))
            .ForMember(nameof(Pager.TotalItemCount), opt => opt.Ignore());
    }
}
