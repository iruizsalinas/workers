namespace Workers;

/// <summary>Represents a Dynamic Dispatch binding.</summary>
public interface IDynamicDispatcherBinding : IBinding
{
    /// <summary>Gets a fetcher for a Worker inside the dispatch namespace.</summary>
    IServiceBinding Get(string name);
}
