using System.Reflection;
using HytaleModLister.Api.Services;
using Serilog;
using Serilog.Events;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    string version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "unknown";
    Log.Information("Starting HytaleModLister API v{Version}", version);

    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    // Use Serilog
    builder.Host.UseSerilog();

    // Add services to the container
    builder.Services.AddControllers();
    builder.Services.AddOpenApi();

    // Configure CORS
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            string[] origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? ["http://localhost:3000"];
            policy.WithOrigins(origins)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
    });

    // Register services
    builder.Services.AddSingleton<ICacheService, CacheService>();
    builder.Services.AddSingleton<IUrlOverrideService, UrlOverrideService>();
    builder.Services.AddSingleton<IModExtractorService, ModExtractorService>();
    builder.Services.AddSingleton<IModMatcherService, ModMatcherService>();
    builder.Services.AddHttpClient<ICurseForgeService, CurseForgeService>();
    builder.Services.AddSingleton<IModRefreshService, ModRefreshService>();
    builder.Services.AddSingleton<RefreshSchedulerService>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<RefreshSchedulerService>());
    builder.Services.AddSingleton<ISessionService, SessionService>();
    builder.Services.AddScoped<IModUpdateService, ModUpdateService>();

    WebApplication app = builder.Build();

    // Configure the HTTP request pipeline
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseCors();
    app.UseAuthorization();
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
