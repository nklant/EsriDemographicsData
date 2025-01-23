using System.Security.Cryptography;
using DemographicsDb.Context;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text;
using System.Web;
using DemographicsBackgroundService.Models;
using DemographicsDb.Models;
using DemographicsLib.Config;

namespace DemographicsBackgroundService.Services;

public class DataFetchingService : IHostedService, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDistributedCache _distributedCache;
    private readonly EndpointOptions _endpointOptions;
    private Timer? _timer;
    private readonly HttpClient _httpClient;
    private readonly CacheSettings _cacheSettings;
    private readonly QuerySettings _querySettings;

    public DataFetchingService(IServiceProvider serviceProvider, 
                               IDistributedCache distributedCache, 
                               IOptions<EndpointOptions> endpointOptions,
                               IOptions<CacheSettings> cacheSettings,
                               IOptions<QuerySettings> querySettings)
    {
        _serviceProvider = serviceProvider;
        _distributedCache = distributedCache;
        _endpointOptions = endpointOptions.Value;
        _httpClient = new HttpClient();
        _cacheSettings = cacheSettings.Value;
        _querySettings = querySettings.Value;
    }

    public Task StartAsync(CancellationToken ct)
    {
        _timer = new Timer(FetchDataAndCache, null, TimeSpan.Zero, TimeSpan.FromMinutes(_cacheSettings.CacheTTLMins));
        return Task.CompletedTask;
    }

    private async void FetchDataAndCache(object? state)
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DemographicDbContext>();

            // Build the URI with query params
            var uriBuilder = new UriBuilder(_endpointOptions.EndpointUri);
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query["where"] = _querySettings.Where;
            query["outFields"] = _querySettings.OutFields;
            query["f"] = _querySettings.F;
            uriBuilder.Query = query.ToString();
            
            var response = await _httpClient.GetStringAsync(uriBuilder.Uri);
            using var jsonData = JsonDocument.Parse(response); // because disposable
            
            // Aggregate by STATE_NAME
            var data = JsonSerializer.Deserialize<FeatureCollection>(response);
            var fetchedData = data?.Features?
                .GroupBy(f => f.Attributes?.StateName)
                .Select(g => new DemographicsData
                {
                    StateName = g.Key ?? "Unknown",
                    Population = g.Sum(x => x.Attributes?.Population ?? 0)
                }).ToList() ?? new List<DemographicsData>();

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
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_cacheSettings.CacheTTLMins)
                };

                await _distributedCache.SetAsync(_cacheSettings.CacheKey, dataToCache, cacheEntryOptions);
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

    public Task StopAsync(CancellationToken ct)
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