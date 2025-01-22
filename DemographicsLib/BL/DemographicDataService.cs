using System.Text;
using System.Text.Json;
using DemographicsDb.Context;
using DemographicsDb.Models;
using DemographicsLib.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace DemographicsLib.BL;

public class DemographicDataService : IDemographicDataService
{
    private readonly DemographicDbContext Db;
    private readonly IDistributedCache _memoryCache;
    
    public DemographicDataService(DemographicDbContext db, IDistributedCache memoryCache)
    {
        Db = db;
        _memoryCache = memoryCache;
    }
    
    public async Task<IEnumerable<DemographicsData>> GetDataAsync(string? stateName)
    {
        byte[]? cachedData = await _memoryCache.GetAsync("DemographicsData").ConfigureAwait(false);

        // If data is cached, return it
        if (cachedData != null)
        {
            string cacheString = Encoding.UTF8.GetString(cachedData);
            List<DemographicsData>? demographicDataCache = JsonSerializer.Deserialize<List<DemographicsData>>(cacheString);

            if (stateName != null)
            {
                demographicDataCache = demographicDataCache?.Where(d => d.StateName == stateName).ToList();
            }

            return demographicDataCache ?? new List<DemographicsData>();
        }
        
        // Else fetch from local DB and re-cache
        var query = Db.DemographicsData.AsNoTracking();

        if (stateName != null)
        {
            query = query.Where(d => d.StateName == stateName);
        }
        
        var data = await query.Select(d => new DemographicsData
        {
            Id = d.Id,
            StateName = d.StateName,
            Population = d.Population
        }).ToListAsync();
        
        string serializedData = JsonSerializer.Serialize(data);
        byte[] dataToCache = Encoding.UTF8.GetBytes(serializedData);
        var cacheEntryOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
        };
        
        await _memoryCache.SetAsync("DemographicsData", dataToCache, cacheEntryOptions).ConfigureAwait(false);
        
        return data;
    }
}