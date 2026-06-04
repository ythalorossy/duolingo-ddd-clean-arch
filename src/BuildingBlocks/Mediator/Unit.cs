namespace BuildingBlocks.Mediator;

// "No meaningful return value" for commands shaped as IRequest<T>.
public readonly record struct Unit
{
    public static readonly Unit Value = new();
}
