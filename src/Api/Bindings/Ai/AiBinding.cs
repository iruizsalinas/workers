namespace Workers;

public interface IAiBinding : IBinding
{
    Task<TOutput?> RunAsync<TInput, TOutput>(string model, TInput input, CancellationToken cancellationToken = default);
    Task<Body> RunBytesAsync<TInput>(string model, TInput input, CancellationToken cancellationToken = default);
}
