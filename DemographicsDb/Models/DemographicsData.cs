namespace DemographicsDb.Models;

public class DemographicsData
{
    public int Id { get; set; }
    public string? StateName { get; set; } = null!;
    public int Population { get; set; }
}