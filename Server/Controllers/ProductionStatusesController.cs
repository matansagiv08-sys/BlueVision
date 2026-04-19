using Microsoft.AspNetCore.Mvc;
using Server.Models;

namespace Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductionStatusesController : ControllerBase
    {
        [HttpGet]
        public IEnumerable<ProductionStatus> GetProductionStatuses()
        {
            ProductionStatus statuses = new ProductionStatus();
            return statuses.GetProductionStatuses();
        }
    }
}
