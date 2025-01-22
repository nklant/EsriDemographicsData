using System.Text;
using System.Text.Json;
using DemographicsDb.Context;
using DemographicsDb.Models;
using DemographicsLib.Config;
using DemographicsLib.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace DemographicsLib.BL;

public class DemographicDataService : IDemographicDataService
{
    private readonly DemographicDbContext Db;
    private readonly IDistributedCache _memoryCache;
    private readonly CacheSettings _cacheSettings;
    
    public DemographicDataService(DemographicDbContext db, IDistributedCache memoryCache, IOptions<CacheSettings> cacheSettings)
    {
        Db = db;
        _memoryCache = memoryCache;
        _cacheSettings = cacheSettings.Value;
    }
    
    public async Task<IEnumerable<DemographicsData>> GetDataAsync(string? stateName)
    {
        stateName = stateName?.ToLower();
        
        byte[]? cachedData = await _memoryCache.GetAsync(_cacheSettings.CacheKey).ConfigureAwait(false);
        
        List<DemographicsData>? res;

        // Check if data is cached first and if not, re-cache it from local DB
        if (cachedData == null)
        {
            res = await Db.DemographicsData.AsNoTracking()
                             .Select(d => new DemographicsData
                             {
                                 Id = d.Id,
                                 StateName = d.StateName,
                                 Population = d.Population
                             }).ToListAsync();
            
            string serializedData = JsonSerializer.Serialize(res);
            byte[] dataToCache = Encoding.UTF8.GetBytes(serializedData);
            var cacheEntryOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_cacheSettings.CacheTTLMins)
            };

            await _memoryCache.SetAsync(_cacheSettings.CacheKey, dataToCache, cacheEntryOptions).ConfigureAwait(false);
        }
        else
        {
            string serializedData = Encoding.UTF8.GetString(cachedData);
            res = JsonSerializer.Deserialize<List<DemographicsData>>(serializedData);
        }
        
        if (!string.IsNullOrEmpty(stateName))
        {
            res = res?.Where(d => d.StateName != null && d.StateName.ToLower().Contains(stateName)).ToList();
        }
        
        return res ?? new();
    }
}