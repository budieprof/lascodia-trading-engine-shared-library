namespace Lascodia.Trading.Engine.SharedLibrary;

public class PagerRequestWithFilterType<TFilter, TResponse> : PagerRequest<TResponse>
{
    public new TFilter? Filter
        {
            set => base.Filter = value == null ? null : Helper.GetJson(value);
        }
    
}
