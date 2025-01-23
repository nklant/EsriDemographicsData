namespace DemographicsLib.Config;

public class QuerySettings
{
    public string Where { get; set; } = null!;
    public string OutFields { get; set; } = null!;
    public string ReturnGeometry { get; set; } = null!;
    public string F { get; set; } = null!;
}