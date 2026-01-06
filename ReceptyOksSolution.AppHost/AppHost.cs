var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.ReceptyOks_Api>("receptyoks-api");

builder.AddProject<Projects.ReceptyOks>("receptyoks")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
