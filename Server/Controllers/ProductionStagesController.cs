using Microsoft.AspNetCore.Mvc;
using Server.Models;

namespace Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductionStagesController : ControllerBase
    {
        [HttpGet]
        public IEnumerable<ProductionStage> GetProductionStages()
        {
            ProductionStage stage = new ProductionStage();
            return stage.GetProductionStages();
        }
    }
}
