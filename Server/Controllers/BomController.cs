using Microsoft.AspNetCore.Mvc;
using Server.DAL;
using Server.Models;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BomController : ControllerBase
{
    [HttpGet]
    public IEnumerable<BomRow> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        [FromQuery] int? planeTypeId = null,
        [FromQuery] string? search = null,
        [FromQuery] string? measureUnit = null,
        [FromQuery] string? warehouse = null,
        [FromQuery] int? bomLevel = null,
        [FromQuery] bool? hasChild = null,
        [FromQuery] string? buyMethod = null,
        [FromQuery] string? bodyPlane = null)
    {
        DBservices dbs = new DBservices();
        return dbs.GetBomRows(page, pageSize, planeTypeId, search, measureUnit, warehouse, bomLevel, hasChild, buyMethod, bodyPlane);
    }

    [HttpGet("planes")]
    public IEnumerable<BomPlaneOption> GetPlaneOptions()
    {
        DBservices dbs = new DBservices();
        return dbs.GetBomPlaneOptions();
    }

    [HttpGet("filter-options")]
    public BomFilterOptions GetFilterOptions([FromQuery] int? planeTypeId = null)
    {
        DBservices dbs = new DBservices();
        return dbs.GetBomFilterOptions(planeTypeId);
    }
}
