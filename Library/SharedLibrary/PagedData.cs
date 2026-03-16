namespace Lascodia.Trading.Engine.SharedLibrary;

public class PagedData<T>
{
    public required Pager pager { get; set; }
    public required List<T> data { get; set; }
}
