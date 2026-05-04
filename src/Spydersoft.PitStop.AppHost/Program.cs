var builder = DistributedApplication.CreateBuilder(args);

var db = builder.AddPostgres("pitstop-db")
    .AddDatabase("pitstop-db");

builder.AddProject<Projects.Spydersoft_PitStop_Api>("api")
    .WithReference(db)
    .WaitFor(db);

builder.Build().Run();
