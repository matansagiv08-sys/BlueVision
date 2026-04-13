using Microsoft.AspNetCore.Mvc;
using Server.Models;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryItemsController : ControllerBase
{
    [HttpGet]
    public IEnumerable<InventoryItem> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        [FromQuery] string? search = null,
        [FromQuery] string? stockStatus = "all",
        [FromQuery] int? planeTypeId = null,
        [FromQuery] int? itemGrpID = null,
        [FromQuery] string? buyMethod = null,
        [FromQuery] int? supplierID = null,
        [FromQuery] string? bodyPlane = null,
        [FromQuery] DateTime? lastPODate = null)
    {
        InventoryItem inventoryItem = new InventoryItem();
        return inventoryItem.GetInventoryItems(page, pageSize, search, stockStatus, planeTypeId, itemGrpID, buyMethod, supplierID, bodyPlane, lastPODate);
    }

    [HttpGet("filter-options")]
    public InventoryFilterOptions GetFilterOptions()
    {
        InventoryItem inventoryItem = new InventoryItem();
        return inventoryItem.GetInventoryFilterOptions();
    }
}
