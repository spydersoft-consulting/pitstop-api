var builder = DistributedApplication.CreateBuilder(args);

var db = builder.AddPostgres("postgres")
    .AddDatabase("pitstop-db");

var dashboardOtlp = builder.Configuration["DOTNET_DASHBOARD_OTLP_ENDPOINT_URL"]
    ?? "http://localhost:18889";

var api = builder.AddProject<Projects.Spydersoft_PitStop_Api>("api")
    .WithReference(db)
    .WaitFor(db);

foreach (var (typeKey, endpointKey) in new[]
{
    ("Telemetry__Trace__Type",   "Telemetry__Trace__Otlp__Endpoint"),
    ("Telemetry__Metrics__Type", "Telemetry__Metrics__Otlp__Endpoint"),
    ("Telemetry__Log__Type",     "Telemetry__Log__Otlp__Endpoint"),
})
{
    api.WithEnvironment(typeKey, builder.Configuration[typeKey] ?? "otlp");
    api.WithEnvironment(endpointKey, builder.Configuration[endpointKey] ?? dashboardOtlp);
}

if (builder.Environment.EnvironmentName == "Testing")
{
    var testKey = builder.Configuration["Auth:TestKey"]
        ?? "jRv3YFPH/19t9t5CgsEFgAkykfW5bQhHmceMprLgzlQ=";

    api.WithEnvironment("DOTNET_ENVIRONMENT", "Testing")
       .WithEnvironment("Auth__TestKey", testKey);

    builder.AddProject<Projects.Spydersoft_PitStop_DataSeeder>("data-seeder")
        .WithReference(db)
        .WaitFor(api)
        .WithEnvironment("PITSTOP_TEST_KEY", testKey);
}

await builder.Build().RunAsync();
