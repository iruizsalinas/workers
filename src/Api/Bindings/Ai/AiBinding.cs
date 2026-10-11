namespace Workers;

public interface IAiBinding : IBinding
{
    Task<TOutput?> RunAsync<TInput, TOutput>(string model, TInput input);
    Task<ReadableStream> RunBytesAsync<TInput>(string model, TInput input);
}
