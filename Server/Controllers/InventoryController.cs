using Microsoft.AspNetCore.Mvc;
using Server.Models;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    [HttpPost("import")]
    public ActionResult Import()
    {
        try
        {
            InventoryItem inventoryItem = new InventoryItem();
            int importedRows = inventoryItem.ImportFromExcel(null);

            return Ok(new
            {
                message = "Inventory import completed successfully",
                importedRows
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                message = "Inventory import failed.",
                details = ex.Message
            });
        }
    }
}
