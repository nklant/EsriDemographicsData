using System.Text.Json.Serialization;

namespace DemographicsBackgroundService.Models;

public class FeatureCollection
{
    [JsonPropertyName("features")]
    public List<Feature>? Features { get; set; }
}

public class Feature
{
    [JsonPropertyName("attributes")]
    public Attributes? Attributes { get; set; }
}

public class Attributes
{
    [JsonPropertyName("STATE_NAME")]
    public string? StateName { get; set; }
    [JsonPropertyName("POPULATION")]
    public long? Population { get; set; }
}