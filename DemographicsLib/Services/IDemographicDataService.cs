using DemographicsDb.Models;

namespace DemographicsLib.Services;

public interface IDemographicDataService
{
    Task<IEnumerable<DemographicsData>> GetDataAsync(string? stateName);
}