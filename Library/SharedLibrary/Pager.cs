using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace Lascodia.Trading.Engine.SharedLibrary;

[ExcludeFromCodeCoverage]
public class Pager
{

    public int TotalItemCount { get; set; }
    public string? Filter { get; set; }

    /// <summary>
    /// Client-requested sort column (forwarded from
    /// <see cref="PagerRequest{TResponse}.SortBy"/> by AutoMapper). Each query
    /// handler decides whether to honour it via
    /// <see cref="ApplySort{T}(IQueryable{T}, IReadOnlyDictionary{string, Expression{Func{T, object}}}, Expression{Func{T, object}}?, bool)"/>,
    /// <see cref="ApplySortByName{T}(IQueryable{T}, IEnumerable{string}, Expression{Func{T, object}}?, bool)"/>,
    /// or <see cref="ApplyAutoSort{T}(IQueryable{T}, Expression{Func{T, object}}?, bool)"/>.
    /// All three guard against arbitrary client property names reaching EF.
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary><c>"asc"</c> or <c>"desc"</c>. Defaults to <c>"desc"</c>.</summary>
    public string? SortDirection { get; set; }

    public int CurrentPage { get; set; } = 1;

    public int ItemCountPerPage { get; set; } = 5;

    private int NowViewing
    {
        get { return CurrentPage - 1; }
    }

    public int SerialNo(int index)
    {
        return (NowViewing * PageSize) + (index + 1);
    }
    public int PageSize
    {
        get { return ItemCountPerPage; }
    }
    public int PageNo
    {
        get
        {
            if (ItemCountPerPage == 0) return 1;
            return (int)Math.Ceiling(((decimal)TotalItemCount / ItemCountPerPage));
        }
    }

    public IQueryable<T> ExecuteQuery<T>(IQueryable<T> value) where T : class
    {
        if (PageSize == 0)
        {
            CurrentPage = 1;
            return value;
        }
        var startingPoint = GetStartingPoint();
        TotalItemCount = value.Count();
        var t = value.Skip(startingPoint).Take(PageSize);
        return t;
    }
    public IEnumerable<T> ExecuteQuery<T>(IEnumerable<T> value) where T : class
    {
        if (PageSize == 0)
        {
            CurrentPage = 1;
            return value;
        }
        var startingPoint = GetStartingPoint();
        TotalItemCount = value.Count();
        var t = value.Skip(startingPoint).Take(PageSize);
        return t;
    }
    public int GetStartingPoint()
    {
        return NowViewing * PageSize;
    }


    public PagedData<T> GetListPagedData<T>(List<T> value)
    {
        Pager pager = this;
        var result = new PagedData<T> { pager = pager, data = value };
        return result;
    }

    /// <summary>
    /// Applies the client-requested sort to <paramref name="query"/> when
    /// <see cref="SortBy"/> matches a key in <paramref name="safelist"/>;
    /// otherwise returns <paramref name="query"/> ordered by
    /// <paramref name="defaultOrder"/> (when supplied) or unchanged.
    /// </summary>
    /// <typeparam name="T">Entity type being sorted.</typeparam>
    /// <param name="query">The base IQueryable.</param>
    /// <param name="safelist">
    /// Map of client-facing column names to entity property selectors.
    /// Keys are matched case-insensitively. Anything outside the safelist
    /// is silently dropped — guards against arbitrary client property names
    /// reaching the EF query (and from there, surfacing un-indexed columns
    /// or projecting sensitive fields into ORDER BY).
    /// </param>
    /// <param name="defaultOrder">
    /// Order to apply when <see cref="SortBy"/> is absent or unrecognised.
    /// Pass <c>null</c> if the caller will ordered the query itself.
    /// </param>
    /// <param name="defaultDescending">
    /// Direction for <paramref name="defaultOrder"/>. Defaults to descending
    /// (most-recent-first) which matches the historical hard-coded order on
    /// most list endpoints.
    /// </param>
    public IQueryable<T> ApplySort<T>(
        IQueryable<T> query,
        IReadOnlyDictionary<string, Expression<Func<T, object>>> safelist,
        Expression<Func<T, object>>? defaultOrder = null,
        bool defaultDescending = true)
    {
        Expression<Func<T, object>>? selector = null;
        if (!string.IsNullOrWhiteSpace(SortBy))
        {
            foreach (var kv in safelist)
            {
                if (string.Equals(kv.Key, SortBy, StringComparison.OrdinalIgnoreCase))
                {
                    selector = kv.Value;
                    break;
                }
            }
        }

        bool descending = !string.Equals(SortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        if (selector is not null)
        {
            return descending ? query.OrderByDescending(selector) : query.OrderBy(selector);
        }

        if (defaultOrder is not null)
        {
            return defaultDescending ? query.OrderByDescending(defaultOrder) : query.OrderBy(defaultOrder);
        }

        return query;
    }

    /// <summary>
    /// Ergonomic overload of <see cref="ApplySort"/> that takes a string
    /// allowlist of property names and reflects the matching property on
    /// <typeparamref name="T"/>. Caller passes only the names of sortable
    /// columns; the EF expression is composed at call time. Same safety
    /// guarantee as the dictionary overload — only allowlisted names hit EF.
    /// </summary>
    /// <remarks>
    /// Match is case-insensitive so the ag-grid <c>colId</c> (camelCase from
    /// the DTO/entity property) lines up with the Pascal-cased property on
    /// the entity. Property names that don't exist on <typeparamref name="T"/>
    /// (or aren't in the allowlist) fall through to <paramref name="defaultOrder"/>.
    /// </remarks>
    public IQueryable<T> ApplySortByName<T>(
        IQueryable<T> query,
        IEnumerable<string> allowedPropertyNames,
        Expression<Func<T, object>>? defaultOrder = null,
        bool defaultDescending = true)
    {
        if (!string.IsNullOrWhiteSpace(SortBy))
        {
            var allowed = new HashSet<string>(allowedPropertyNames, StringComparer.OrdinalIgnoreCase);
            if (allowed.Contains(SortBy))
            {
                var sorted = TryBuildSortedQuery(query);
                if (sorted is not null) return sorted;
            }
        }

        if (defaultOrder is not null)
        {
            return defaultDescending ? query.OrderByDescending(defaultOrder) : query.OrderBy(defaultOrder);
        }

        return query;
    }

    /// <summary>
    /// Auto-allowlisted sort: any primitive / enum / DateTime / string /
    /// decimal / Guid property on <typeparamref name="T"/> is sortable, with
    /// nullable variants of those types also accepted. Navigation properties,
    /// collections, and complex objects are excluded so a sort can't traverse
    /// graph relationships or project sensitive subobjects into ORDER BY.
    /// </summary>
    /// <remarks>
    /// Designed for the admin-UI pattern where every paginated handler wants
    /// every column on the listed entity to be sortable without maintaining
    /// a per-handler allowlist. Endpoints are operator-auth-gated, ORDER BY
    /// is read-only, and the type filter prevents the pathological cases.
    /// Handlers needing stricter control should use
    /// <see cref="ApplySortByName{T}(IQueryable{T}, IEnumerable{string}, Expression{Func{T, object}}?, bool)"/>
    /// or
    /// <see cref="ApplySort{T}(IQueryable{T}, IReadOnlyDictionary{string, Expression{Func{T, object}}}, Expression{Func{T, object}}?, bool)"/>
    /// instead.
    /// </remarks>
    public IQueryable<T> ApplyAutoSort<T>(
        IQueryable<T> query,
        Expression<Func<T, object>>? defaultOrder = null,
        bool defaultDescending = true)
    {
        if (!string.IsNullOrWhiteSpace(SortBy))
        {
            var sorted = TryBuildSortedQuery(query);
            if (sorted is not null) return sorted;
        }

        if (defaultOrder is not null)
        {
            return defaultDescending ? query.OrderByDescending(defaultOrder) : query.OrderBy(defaultOrder);
        }

        return query;
    }

    private IQueryable<T>? TryBuildSortedQuery<T>(IQueryable<T> query)
    {
        var prop = typeof(T)
            .GetProperties()
            .FirstOrDefault(p => string.Equals(p.Name, SortBy, StringComparison.OrdinalIgnoreCase));
        if (prop is null) return null;
        if (!IsSortablePropertyType(prop.PropertyType)) return null;

        var param = Expression.Parameter(typeof(T), "x");
        Expression access = Expression.Property(param, prop);
        // EF needs a uniform Func<T, object> shape; cast the property value
        // (any T including value types) to object.
        if (access.Type.IsValueType)
            access = Expression.Convert(access, typeof(object));
        var lambda = Expression.Lambda<Func<T, object>>(access, param);

        bool descending = !string.Equals(SortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        return descending ? query.OrderByDescending(lambda) : query.OrderBy(lambda);
    }

    private static bool IsSortablePropertyType(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        return t.IsPrimitive
            || t.IsEnum
            || t == typeof(string)
            || t == typeof(DateTime)
            || t == typeof(DateTimeOffset)
            || t == typeof(decimal)
            || t == typeof(Guid)
            || t == typeof(TimeSpan);
    }
}
