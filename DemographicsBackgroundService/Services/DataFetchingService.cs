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
            _timer = new Timer(FetchAndCacheAsync, null, TimeSpan.Zero, TimeSpan.FromMinutes(_cacheSettings.CacheTTLMins));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "StartAsync.FetchDataAndCache");
        }
        
        return Task.CompletedTask;
    }

    private async void FetchAndCacheAsync(object? state)
    {
        using var scope = _serviceProvider.CreateScope();
        var Db = scope.ServiceProvider.GetRequiredService<DemographicDbContext>();
    
        int maxRecordCount = await GetMaxRecordCountAsync();
    
        var allData = await FetchAllFeaturesAsync(maxRecordCount);
    
        var processedData = ProcessFetchedData(allData);
    
        bool isUpdated = await UpdateDbAsync(Db, processedData);
    
        if (isUpdated)
        {
            await UpdateCacheAsync(processedData);
        }
    }

    private async Task<int> GetMaxRecordCountAsync()
    {
        var response = await _httpClient.GetStringAsync(_endpointOptions.EndpointUri + "?f=json");
        using var jsonDocument = JsonDocument.Parse(response);

        return jsonDocument.RootElement.TryGetProperty("maxRecordCount", out var element) ? element.GetInt32() : 1000;
    }
    
    private List<DemographicsData> ProcessFetchedData(List<Feature> features)
    {
        return features?
            .GroupBy(f => f.Attributes?.StateName)
            .Select(g => new DemographicsData
            {
                StateName = g.Key ?? "Unknown",
                Population = g.Sum(x => x.Attributes?.Population ?? 0)
            }).ToList() ?? new();
    }
    
    private async Task<List<Feature>> FetchAllFeaturesAsync(int maxRecordCount)
    {
        var features = new List<Feature>();
        int offset = 0;

        var uriBuilder = new UriBuilder(_endpointOptions.EndpointUri)
        {
            Path = new Uri(_endpointOptions.EndpointUri).AbsolutePath.TrimEnd('/') + "/query"
        };
        var query = HttpUtility.ParseQueryString(uriBuilder.Query);
        query["where"] = QuerySettings.Where;
        query["outFields"] = QuerySettings.OutFields;
        query["returnGeometry"] = QuerySettings.ReturnGeometry;
        query["f"] = QuerySettings.F;

        while (true)
        {
            query["resultOffset"] = offset.ToString();
            query["resultRecordCount"] = maxRecordCount.ToString();
            uriBuilder.Query = query.ToString();

            var response = await _httpClient.GetStringAsync(uriBuilder.Uri);
            var jsonData = JsonSerializer.Deserialize<FeatureCollection>(response);

            if (jsonData?.Features != null)
            {
                features.AddRange(jsonData.Features);
            }

            int recordCount = jsonData?.Features?.Count ?? 0;
            if (recordCount < maxRecordCount)
            {
                break;
            }

            offset += maxRecordCount;
        }

        return features;
    }
    
    private async Task<bool> UpdateDbAsync(DemographicDbContext Db, List<DemographicsData> fetchedData)
    {
        string fetchedDataHash = ComputeHash(fetchedData);
        string? storedHash = Db.DataHash.SingleOrDefault()?.Hash;

        if (storedHash == fetchedDataHash)
        {
            return false; // No change
        }

        await using var tran = await Db.Database.BeginTransactionAsync();
    
        Db.DemographicsData.RemoveRange(Db.DemographicsData);
        await Db.DemographicsData.AddRangeAsync(fetchedData);

        if (storedHash == null)
        {
            Db.DataHash.Add(new DataHash { Hash = fetchedDataHash });
        }
        else
        {
            Db.DataHash.First().Hash = fetchedDataHash;
        }

        await Db.SaveChangesAsync();
        await tran.CommitAsync();

        return true; // Updated
    }

    private async Task UpdateCacheAsync(List<DemographicsData> fetchedData)
    {
        string serializedData = JsonSerializer.Serialize(fetchedData);
        byte[] dataToCache = Encoding.UTF8.GetBytes(serializedData);

        var cacheEntryOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_cacheSettings.CacheTTLMins)
        };

        await _distributedCache.SetAsync(_cacheSettings.CacheKey, dataToCache, cacheEntryOptions);
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