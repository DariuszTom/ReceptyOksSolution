var builder = DistributedApplication.CreateBuilder(args);

// Fixed host port so tools (SSMS, sqlpackage, scripts\Import-ProductionDatabase.ps1) can connect.
var sql = builder.AddSqlServer("sql", port: 14330)
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume("receptyoks-sql-data");

// Resource name must match DatabaseExtensions.DevelopmentConnectionStringName in the API.
var database = sql.AddDatabase("receptyoksdb", "ReceptyOks");

var api = builder.AddProject<Projects.ReceptyOks_Api>("receptyoks-api")
    .WithReference(database)
    .WaitFor(database);

builder.AddProject<Projects.ReceptyOks>("receptyoks")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
