using System.Diagnostics;
using System.Text.Json;

namespace Workers.Compiler.Tests;

// Runs a compiled Worker's fetch handler in Node.js, so tests assert what the generated code does
// rather than how it is spelled. Node provides the Request, Response and URL globals it needs.
internal static class JavaScriptRunner
{
    public static JsonElement FetchJson(string source, string url = "https://worker.test/")
    {
        var module = Compile(source);
        var directory = Directory.CreateTempSubdirectory("workers-run-");
        try
        {
            var path = Path.Combine(directory.FullName, "worker.mjs");
            System.IO.File.WriteAllText(path, module);
            var start = new ProcessStartInfo("node")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var argument in new[] { "--input-type=module", "-e", Script, new Uri(path).AbsoluteUri, url })
                start.ArgumentList.Add(argument);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start node.");
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"The Worker failed in Node.js:{Environment.NewLine}{error}{Environment.NewLine}{module}");
            return JsonDocument.Parse(output.Result).RootElement.Clone();
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private const string Script = """
        const [moduleUrl, url] = process.argv.slice(1);
        // Workers globals that Node.js does not provide.
        globalThis.scheduler ??= { wait: milliseconds => new Promise(resolve => setTimeout(resolve, milliseconds)) };
        const { default: worker } = await import(moduleUrl);
        const context = { waitUntil() {}, passThroughOnException() {} };
        const response = await worker.fetch(new Request(url), {}, context);
        process.stdout.write(await response.text());
        """;
}
