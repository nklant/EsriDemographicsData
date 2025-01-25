using DemographicsDb.Models;
using DemographicsLib.Services;
using Microsoft.AspNetCore.Mvc;

namespace DemographicsWebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DemographicDataController : ControllerBase
{
    private readonly IDemographicDataService _service;
    private readonly ILogger _log;

    public DemographicDataController(IDemographicDataService service, ILogger<DemographicDataController> log)
    {
        _service = service;
        _log = log;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DemographicsData>>> GetDataAsync([FromQuery] string? stateName)
    {
        try
        {
            var data = await _service.GetDataAsync(stateName);
            return Ok(data);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "GetDataAsync");
            return StatusCode(500, ex.Message);
        }
    }
}