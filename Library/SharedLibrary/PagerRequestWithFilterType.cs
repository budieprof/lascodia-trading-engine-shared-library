namespace Lascodia.Trading.Engine.SharedLibrary;

/// <summary>
/// A paged request whose filter shape can be validated without knowing its filter type.
/// <para>
/// The generic parameter is what makes the check possible, and it is only available on the request
/// itself — so the request exposes a non-generic entry point and the pipeline behaviour that guards
/// every paged endpoint can call it without reflecting over type arguments.
/// </para>
/// </summary>
public interface IFilterShapeCheckable
{
    /// <summary>An operator-facing message when filter criteria were sent flat, else <c>null</c>.</summary>
    string? CheckFilterShape();
}

public class PagerRequestWithFilterType<TFilter, TResponse> : PagerRequest<TResponse>, IFilterShapeCheckable
{
    public new TFilter? Filter
        {
            set => base.Filter = value == null ? null : Helper.GetJson(value);
        }

    /// <inheritdoc />
    public string? CheckFilterShape() => FilterShapeError<TFilter>();
}
