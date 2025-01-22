namespace DemographicsLib.Config;

public class CacheSettings
{
    public string CacheKey { get; set; } = null!;
    public int CacheTTLMins { get; set; }
}