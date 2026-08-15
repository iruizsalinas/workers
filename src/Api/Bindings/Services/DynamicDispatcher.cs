namespace Workers;

public interface IDynamicDispatcherBinding : IBinding
{
    IServiceBinding Get(string name);
}
