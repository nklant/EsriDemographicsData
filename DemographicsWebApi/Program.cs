using DemographicsBackgroundService.Services;
using DemographicsDb.Context;
using DemographicsLib.BL;
using DemographicsLib.Config;
using DemographicsLib.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<DemographicDbContext>(options => options.UseSqlServer(connectionString));

var serilog = new LoggerConfiguration()
    .MinimumLevel.Verbose()
    .WriteTo.Logger(lc => lc.Filter.ByIncludingOnly(e => e.Level == LogEventLevel.Information)
    .WriteTo.File("logs/Info-.txt", rollingInterval: RollingInterval.Day))
    .WriteTo.Logger(lc => lc.Filter.ByIncludingOnly(e => e.Level == LogEventLevel.Warning)
    .WriteTo.File("logs/Warning-.txt", rollingInterval: RollingInterval.Day))
    .WriteTo.Logger(lc => lc.Filter.ByIncludingOnly(e => e.Level == LogEventLevel.Error)
    .WriteTo.File("logs/Error-.txt", rollingInterval: RollingInterval.Day))
    .CreateLogger();

builder.Logging.AddSerilog(serilog);

builder.Services.AddControllers();

// Configure in-memory cache
builder.Services.AddDistributedMemoryCache();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Demographics API", Version = "v1" });
});

builder.Services.AddHostedService<DataFetchingService>();
builder.Services.AddScoped<IDemographicDataService, DemographicDataService>();

// Register the endpoint configuration
builder.Services.Configure<EndpointOptions>(builder.Configuration.GetSection("Endpoint"));
// Register the cache settings
builder.Services.Configure<CacheSettings>(builder.Configuration.GetSection("CacheSettings"));

var app = builder.Build();

// Apply migrations at startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DemographicDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Demographics API v1");
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();