using Microsoft.AspNetCore.Mvc;
using Server.DAL;
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
            DBservices dbs = new DBservices();
            return dbs.GetProductionStages();
        }
    }
}
