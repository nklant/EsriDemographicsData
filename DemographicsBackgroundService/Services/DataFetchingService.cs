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
using Microsoft.Extensions.Logging;

namespace DemographicsBackgroundService.Services;

public class DataFetchingService : IHostedService, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDistributedCache _distributedCache;
    private readonly EndpointOptions _endpointOptions;
    private Timer? _timer;
    private readonly HttpClient _httpClient;
    private readonly CacheSettings _cacheSettings;
    private readonly ILogger<DataFetchingService> _log;

    public DataFetchingService(IServiceProvider serviceProvider, 
                               IDistributedCache distributedCache, 
                               IOptions<EndpointOptions> endpointOptions,
                               IOptions<CacheSettings> cacheSettings,
                               ILogger<DataFetchingService> log)
    {
        _serviceProvider = serviceProvider;
        _distributedCache = distributedCache;
        _endpointOptions = endpointOptions.Value;
        _httpClient = new HttpClient();
        _cacheSettings = cacheSettings.Value;
        _log = log;
    }

    public Task StartAsync(CancellationToken ct)
    {
        try
        {
            _timer = new Timer(FetchDataAndCache, null, TimeSpan.Zero, TimeSpan.FromMinutes(_cacheSettings.CacheTTLMins));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "StartAsync.FetchDataAndCache");
        }
        
        return Task.CompletedTask;
    }

    private async void FetchDataAndCache(object? state)
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var Db = scope.ServiceProvider.GetRequiredService<DemographicDbContext>();

            // Build URI with query params
            var uriBuilder = new UriBuilder(_endpointOptions.EndpointUri)
            {
                Path = new Uri(_endpointOptions.EndpointUri).AbsolutePath.TrimEnd('/') + "/query" // Ensure no trailing slash
            };
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query["where"] = QuerySettings.Where;
            query["outFields"] = QuerySettings.OutFields;
            query["returnGeometry"] = QuerySettings.ReturnGeometry;
            query["f"] = QuerySettings.F;
            uriBuilder.Query = query.ToString();
            
            var response = await _httpClient.GetStringAsync(uriBuilder.Uri);
            using var jsonData = JsonDocument.Parse(response); // Because disposable
            
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
            var storedHash = Db.DataHash.Single()?.Hash;

            // Check to see if there is a difference between the stored data and the fetched data
            if (storedHash != fetchedDataHash)
            {
                using (var tran = await Db.Database.BeginTransactionAsync())
                {
                    // Clear old
                    Db.DemographicsData.RemoveRange(Db.DemographicsData);
                    // Add new
                    await Db.DemographicsData.AddRangeAsync(fetchedData);
                    
                    // Update hash
                    if (storedHash == null)
                    {
                        Db.DataHash.Add(new DataHash { Hash = fetchedDataHash });
                    }
                    else
                    {
                        Db.DataHash.First().Hash = fetchedDataHash;
                    }
                    
                    // Save
                    await Db.SaveChangesAsync();
                    await tran.CommitAsync();
                }
                
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