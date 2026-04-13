using Microsoft.AspNetCore.Mvc;
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
        Bom bom = new Bom();
        return bom.GetBomRows(page, pageSize, planeTypeId, search, measureUnit, warehouse, bomLevel, hasChild, buyMethod, bodyPlane);
    }

    [HttpGet("planes")]
    public IEnumerable<BomPlaneOption> GetPlaneOptions()
    {
        Bom bom = new Bom();
        return bom.GetBomPlaneOptions();
    }

    [HttpGet("filter-options")]
    public BomFilterOptions GetFilterOptions([FromQuery] int? planeTypeId = null)
    {
        Bom bom = new Bom();
        return bom.GetBomFilterOptions(planeTypeId);
    }
}
