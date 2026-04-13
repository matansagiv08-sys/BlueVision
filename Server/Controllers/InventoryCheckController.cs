using Microsoft.AspNetCore.Mvc;
using Server.DAL;
using Server.Models;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryCheckController : ControllerBase
{
    [HttpPost("calculate")]
    public ActionResult<InventoryCheckResponse> Calculate([FromBody] InventoryCheckRequest request)
    {
        DBservices dbs = new DBservices();
        InventoryCheckResponse response = dbs.CalculateInventoryCheck(request);
        return Ok(response);
    }
}
    