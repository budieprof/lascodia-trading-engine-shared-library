using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Http;
using Lascodia.Trading.Engine.SharedLibrary.Mappings;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Reflection;
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

    /// <summary>
    /// Top-level JSON members that matched no property on this request.
    /// <para>
    /// Captured rather than discarded because discarding them cost a live misinformation incident.
    /// A paged request carries its criteria in <see cref="Filter"/>; a caller that sends them flat
    /// (<c>{"from": …, "to": …}</c> instead of <c>{"filter": {"from": …, "to": …}}</c>) previously
    /// had them dropped in silence by System.Text.Json. The request still succeeded, the filter
    /// stayed <c>"{}"</c>, and the handler answered with page 1 of the entire unfiltered table.
    /// Wire's calendar tool did exactly this and reported the oldest rows on record as proof the
    /// calendar was not loaded. A wrong answer delivered confidently is worse than a rejection, so
    /// <see cref="FilterShapeError{T}"/> now turns that case into one.
    /// </para>
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? ExtraMembers { get; set; }
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

    /// <summary>
    /// Detects the flat-filter mistake: criteria sent at the top level, where they bind to nothing.
    /// Returns an operator-facing message naming the offending members, or <c>null</c> when the
    /// request is well-shaped.
    /// </summary>
    /// <remarks>
    /// Deliberately narrow. It fires only when an unbound member shares a name with a property of
    /// <typeparamref name="T"/> — that is unambiguously a filter field sent to the wrong place. Any
    /// other unknown member (a client sending a field the server has since dropped, a forward-compatible
    /// caller) is still ignored, so this cannot break a caller that is merely out of date.
    /// </remarks>
    public string? FilterShapeError<T>()
    {
        if (ExtraMembers is null || ExtraMembers.Count == 0) return null;

        var filterFields = typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var misplaced = ExtraMembers.Keys
            .Where(filterFields.Contains)
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (misplaced.Count == 0) return null;

        return
            $"Filter criteria were sent at the top level and would have been ignored: {string.Join(", ", misplaced)}. " +
            $"Nest them under \"filter\": {{ \"filter\": {{ \"{misplaced[0].ToLowerInvariant()}\": … }}, \"currentPage\": 1, \"itemCountPerPage\": 50 }}. " +
            "Rejecting this rather than returning the whole unfiltered table on purpose — a silently dropped filter reads as real data.";
    }

    public void Mapping(Profile profile, IHttpContextAccessor httpContextAssessor)
    {
        profile.CreateMap(GetType(), typeof(Pager))
            .ForMember(nameof(Pager.TotalItemCount), opt => opt.Ignore());
    }
}
