using DemographicsBackgroundService.Models;
using DemographicsBackgroundService.Services;
using DemographicsDb.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") + ";TrustServerCertificate=True";
builder.Services.AddDbContext<DemographicDbContext>(options => options.UseSqlServer(connectionString));

builder.Services.AddControllers();

// Configure in-memory cache
builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Demographics API", Version = "v1" });
});

builder.Services.AddSingleton<IHostedService, DataFetchingService>();

// Register the endpoint configuration
builder.Services.Configure<EndpointOptions>(builder.Configuration.GetSection("Endpoint"));

var app = builder.Build();

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