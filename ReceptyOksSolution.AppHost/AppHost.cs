var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.ReceptyOks>("receptyoks");

builder.Build().Run();
