using Microsoft.CodeAnalysis;

internal sealed partial class JavaScriptEmitter
{
    private string EmitDatabaseConnect(
        string driver,
        IMethodSymbol method,
        IReadOnlyList<(IParameterSymbol Parameter, string Value)> arguments)
    {
        var source = arguments[0].Value;
        var hyperdrive = method.Parameters[0].Type.ToDisplayString() == "Workers.IHyperdriveBinding";
        switch (driver)
        {
            case "pg":
                var client = _imports.Require("pg", "Client", "PostgresClient");
                var connectionString = hyperdrive ? $"{source}.connectionString" : source;
                // Failures reject the pending connect or query; the listener only absorbs the socket
                // cancellation pg reports as a client error after end().
                return $"(client => (client.on(\"error\", () => {{ }}), client.connect().then(() => client)))(new {client}({{ connectionString: {connectionString} }}))";
            case "mysql2":
                // mysql2 compiles row parsers with eval unless disableEval is set, which Workers forbids.
                var createConnection = _imports.Require("mysql2/promise", "createConnection", "createMySqlConnection");
                var options = hyperdrive
                    ? $"(binding => ({{ host: binding.host, port: binding.port, user: binding.user, password: binding.password, database: binding.database, disableEval: true }}))({source})"
                    : $"{{ uri: {source}, disableEval: true }}";
                return $"{createConnection}({options})";
            default:
                var mongoClient = _imports.Require("mongodb", "MongoClient", "MongoClient");
                return $"new {mongoClient}({source}).connect()";
        }
    }

    private string EmitSqlQuery(
        string receiver,
        IMethodSymbol method,
        string driver,
        IReadOnlyList<(IParameterSymbol Parameter, string Value)> arguments)
    {
        var sql = arguments.Single(item => item.Parameter.Name == "sql").Value;
        var parameters = arguments.FirstOrDefault(item => item.Parameter.Name == "parameters").Value;
        var query = $"{receiver}.query({sql}{(parameters is null ? "" : $", {_helpers.Require(JavaScriptHelper.SqlParameters)}({parameters})")})";
        // pg resolves to a result holding the rows; mysql2 resolves to [rows or result header, fields].
        if (driver == "mysql2") return $"{query}.then(([value]) => value)";
        return method.IsGenericMethod ? $"{query}.then(value => value.rows)" : query;
    }

    private string EmitMongoOperation(
        string receiver,
        string operation,
        IReadOnlyList<(IParameterSymbol Parameter, string Value)> arguments,
        string? cancellation)
    {
        var document = _helpers.Require(JavaScriptHelper.MongoValues);
        var value = _helpers.Name("mongoValue");
        string? Argument(string name) => arguments.FirstOrDefault(item => item.Parameter.Name == name).Value;
        var filter = Argument("filter") is { } supplied ? $"{document}({supplied}) ?? {{}}" : "{}";
        var options = Argument("options") is { } settings ? $", {document}({settings}) ?? undefined" : "";
        // The driver aborts reads through an AbortSignal; writes only take the pre-call check because
        // abandoning one would leave its outcome unknown.
        var signal = cancellation is null ? "" : $", {{ signal: {cancellation} ?? undefined }}";
        var readOptions = cancellation is null ? options
            : Argument("options") is { } readSettings
                ? $", {{ ...{document}({readSettings}), signal: {cancellation} ?? undefined }}"
                : signal;
        return operation switch
        {
            "find" => $"{receiver}.find({filter}{readOptions}).toArray().then(documents => documents.map({value}))",
            "aggregate" => $"{receiver}.aggregate({document}(Array.from({Argument("pipeline")})){signal}).toArray().then(documents => documents.map({value}))",
            "countDocuments" => $"{receiver}.countDocuments({filter}{signal})",
            "insertOne" => $"{receiver}.insertOne({document}({Argument("document")}, true)).then({value})",
            "insertMany" => $"{receiver}.insertMany(Array.from({Argument("documents")}, item => {document}(item, true)))"
                + $".then(result => ({{ insertedCount: result.insertedCount, insertedIds: Object.values(result.insertedIds).map({value}) }}))",
            "replaceOne" => $"{receiver}.replaceOne({filter}, {document}({Argument("replacement")}, true){options}).then({value})",
            "updateOne" or "updateMany" => $"{receiver}.{operation}({filter}, {document}({Argument("update")}){options}).then({value})",
            "findOne" => $"{receiver}.findOne({filter}{signal}).then({value})",
            _ => $"{receiver}.{operation}({filter}).then({value})"
        };
    }
}
