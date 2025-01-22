using DemographicsDb.Models;
using DemographicsLib.Services;
using Microsoft.AspNetCore.Mvc;

namespace DemographicsWebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DemographicDataController : ControllerBase
{
    private readonly IDemographicDataService _service;

    public DemographicDataController(IDemographicDataService service)
    {
        _service = service;
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
            return StatusCode(500, ex.Message);
        }
    }
}