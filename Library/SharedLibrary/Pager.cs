using System.Diagnostics.CodeAnalysis;

namespace Lascodia.Trading.Engine.SharedLibrary;

[ExcludeFromCodeCoverage]
public class Pager
{

    public int TotalItemCount { get; set; }
    public string? Filter { get; set; }

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
}
