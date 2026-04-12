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
            InventoryImportResult importResult = inventoryItem.ImportFromExcel(null);

            return Ok(new
            {
                message = "Inventory import completed successfully",
                importedRows = importResult.ImportedRows,
                deletedProductionItems = importResult.DeletedProductionItems,
                insertedProductionItems = importResult.InsertedProductionItems,
                updatedProductionItems = importResult.UpdatedProductionItems,
                finalProductionItemsCount = importResult.FinalProductionItemsCount
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
