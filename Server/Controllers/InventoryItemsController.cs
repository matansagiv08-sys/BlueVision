using Microsoft.AspNetCore.Mvc;
using Server.DAL;
using Server.Models;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryItemsController : ControllerBase
{
    [HttpGet]
    public IEnumerable<InventoryItem> Get()
    {
        DBservices dbs = new DBservices();
        return dbs.GetInventoryItems();
    }
}
