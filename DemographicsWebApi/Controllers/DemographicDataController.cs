using System.Text;
using DemographicsDb.Context;
using DemographicsWebApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace DemographicsWebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DemographicDataController : ControllerBase
{
    private readonly DemographicDbContext Db;
    private readonly IDistributedCache _memoryCache;

    public DemographicDataController(DemographicDbContext db, IDistributedCache memoryCache)
    {
        Db = db;
        _memoryCache = memoryCache;
    }

    /// <summary>
    /// Get all or filter demographic data by stateName from cache if available, otherwise fetch from database
    /// </summary>
    /// <param name="stateName"></param>
    /// <returns>DemographicsData</returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<DemographicsData>>> Get([FromQuery] string? stateName = null)
    {
        byte[]? cachedData = await _memoryCache.GetAsync("DemographicsData").ConfigureAwait(false);

        if (cachedData != null)
        {
            string cacheString = Encoding.UTF8.GetString(cachedData);
            var demographicDataCache = JsonSerializer.Deserialize<List<DemographicsData>>(cacheString);

            if (stateName != null)
            {
                demographicDataCache = demographicDataCache?.Where(d => d.StateName == stateName).ToList();
            }

            return demographicDataCache ?? new List<DemographicsData>();
        }

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

        return data;
    }
}