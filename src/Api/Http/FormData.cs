namespace Workers;

public sealed class FormData;
public abstract class FormEntry;
public sealed class FormField : FormEntry
{
    public string Value => WorkerApi.NotExecutable<string>();
}

public sealed class FormFile : FormEntry
{
    public string FileName => WorkerApi.NotExecutable<string>();
    public string ContentType => WorkerApi.NotExecutable<string>();
    public Body Body => WorkerApi.NotExecutable<Body>();
}
