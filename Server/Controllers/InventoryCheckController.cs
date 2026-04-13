using Microsoft.AspNetCore.Mvc;
using Server.Models;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryCheckController : ControllerBase
{
    [HttpPost("calculate")]
    public ActionResult<InventoryCheckResponse> Calculate([FromBody] InventoryCheckRequest request)
    {
        InventoryCheck inventoryCheck = new InventoryCheck();
        InventoryCheckResponse response = inventoryCheck.Calculate(request);
        return Ok(response);
    }
}
    
