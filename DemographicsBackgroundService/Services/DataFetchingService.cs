using System.Security.Cryptography;
using DemographicsDb.Context;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text;
using DemographicsBackgroundService.Models;
using DemographicsDb.Models;

namespace DemographicsBackgroundService.Services;

public class DataFetchingService : IHostedService, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDistributedCache _distributedCache;
    private readonly EndpointOptions _endpointOptions;
    private Timer? _timer;
    private readonly HttpClient _httpClient;

    public DataFetchingService(IServiceProvider serviceProvider, IDistributedCache distributedCache, IOptions<EndpointOptions> endpointOptions)
    {
        _serviceProvider = serviceProvider;
        _distributedCache = distributedCache;
        _endpointOptions = endpointOptions.Value;
        _httpClient = new HttpClient();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(FetchDataAndCache, null, TimeSpan.Zero, TimeSpan.FromMinutes(10));
        return Task.CompletedTask;
    }

    private async void FetchDataAndCache(object? state)
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DemographicDbContext>();

            // Fetch data from the endpoint
            var response = await _httpClient.GetStringAsync(new Uri(_endpointOptions.EndpointUri));
            var jsonData = JsonDocument.Parse(response);

            // Aggregate data by STATE_NAME
            var fetchedData = jsonData.RootElement.GetProperty("features")
                .EnumerateArray()
                .GroupBy(feature => feature.GetProperty("attributes").GetProperty("STATE_NAME").GetString())
                .Select(group => new DemographicsData
                {
                    StateName = group.Key,
                    Population = group.Sum(feature =>
                    {
                        var populationElement = feature.GetProperty("attributes").GetProperty("POPULATION");
                        return populationElement.ValueKind == JsonValueKind.Null ? 0 : populationElement.GetInt32();
                    })
                })
                .ToList();

            // Compute hash of the fetched data
            string fetchedDataHash = ComputeHash(fetchedData);

            // Retrieve the stored hash from the database
            var storedHash = context.DataHash.FirstOrDefault()?.Hash;

            if (storedHash != fetchedDataHash)
            {
                // Save to database
                context.DemographicsData.RemoveRange(context.DemographicsData); // Clear existing data
                context.DemographicsData.AddRange(fetchedData);

                // Update the stored hash
                if (storedHash == null)
                {
                    context.DataHash.Add(new DataHash { Hash = fetchedDataHash });
                }
                else
                {
                    context.DataHash.First().Hash = fetchedDataHash;
                }

                await context.SaveChangesAsync();

                // Save to cache
                string serializedData = JsonSerializer.Serialize(fetchedData);
                byte[] dataToCache = Encoding.UTF8.GetBytes(serializedData);

                var cacheEntryOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
                };

                await _distributedCache.SetAsync("DemographicsData", dataToCache, cacheEntryOptions);
            }
        }
    }

    private string ComputeHash(List<DemographicsData> data)
    {
        using (var sha256 = SHA256.Create())
        {
            var json = JsonSerializer.Serialize(data);
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
            return Convert.ToBase64String(hashBytes);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _httpClient.Dispose();
    }
}