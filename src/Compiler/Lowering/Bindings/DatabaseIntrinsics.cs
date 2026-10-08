using static BindingIntrinsicRegistry;

// Database clients map onto the npm drivers Cloudflare documents for Workers: pg, mysql2 and mongodb.
internal static class DatabaseIntrinsics
{
    public static IReadOnlyDictionary<(string Type, string Method), BindingIntrinsic> Methods { get; } =
        new Dictionary<(string Type, string Method), BindingIntrinsic>
        {
            [Key("Workers.PostgresClient", "ConnectAsync")] = new("pg", BindingIntrinsicKind.DatabaseConnect),
            [Key("Workers.PostgresClient", "QueryAsync")] = new("pg", BindingIntrinsicKind.SqlQuery),
            [Key("Workers.PostgresClient", "DisposeAsync")] = Direct("end"),
            [Key("Workers.MySqlClient", "ConnectAsync")] = new("mysql2", BindingIntrinsicKind.DatabaseConnect),
            [Key("Workers.MySqlClient", "QueryAsync")] = new("mysql2", BindingIntrinsicKind.SqlQuery),
            [Key("Workers.MySqlClient", "DisposeAsync")] = Direct("end"),
            [Key("Workers.MongoClient", "ConnectAsync")] = new("mongodb", BindingIntrinsicKind.DatabaseConnect),
            [Key("Workers.MongoClient", "ObjectId")] = new("", BindingIntrinsicKind.MongoObjectId),
            [Key("Workers.MongoClient", "Database")] = Direct("db"),
            [Key("Workers.MongoClient", "DisposeAsync")] = Direct("close"),
            [Key("Workers.MongoDatabase", "Collection")] = Direct("collection"),
            [Key("Workers.MongoCollection<T>", "FindAsync")] = new("find", BindingIntrinsicKind.MongoOperation),
            [Key("Workers.MongoCollection<T>", "FindOneAsync")] = new("findOne", BindingIntrinsicKind.MongoOperation),
            [Key("Workers.MongoCollection<T>", "CountDocumentsAsync")] = new("countDocuments", BindingIntrinsicKind.MongoOperation),
            [Key("Workers.MongoCollection<T>", "AggregateAsync")] = new("aggregate", BindingIntrinsicKind.MongoOperation),
            [Key("Workers.MongoCollection<T>", "InsertOneAsync")] = new("insertOne", BindingIntrinsicKind.MongoOperation),
            [Key("Workers.MongoCollection<T>", "InsertManyAsync")] = new("insertMany", BindingIntrinsicKind.MongoOperation),
            [Key("Workers.MongoCollection<T>", "UpdateOneAsync")] = new("updateOne", BindingIntrinsicKind.MongoOperation),
            [Key("Workers.MongoCollection<T>", "UpdateManyAsync")] = new("updateMany", BindingIntrinsicKind.MongoOperation),
            [Key("Workers.MongoCollection<T>", "ReplaceOneAsync")] = new("replaceOne", BindingIntrinsicKind.MongoOperation),
            [Key("Workers.MongoCollection<T>", "DeleteOneAsync")] = new("deleteOne", BindingIntrinsicKind.MongoOperation),
            [Key("Workers.MongoCollection<T>", "DeleteManyAsync")] = new("deleteMany", BindingIntrinsicKind.MongoOperation)
        };
}
