namespace DemographicsLib.Config;

/// <summary>
/// Contains the query settings for the provided endpoint.
/// </summary>
public class QuerySettings
{
    public const string Where = "1=1";
    public const string OutFields = "population, state_name";
    public const string ReturnGeometry = "false";
    public const string F = "pjson";
}