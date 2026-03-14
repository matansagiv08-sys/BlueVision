using Microsoft.AspNetCore.Mvc;
using Server.Models;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ItemsInProductionController : ControllerBase
    {
        // GET: api/<ItemsInProductionController>
        [HttpGet]
        public IEnumerable<ItemsInProduction> Get()
        {
            ItemsInProduction item = new ItemsInProduction();
            return item.Read();
        }

        // POST api/<ItemsInProductionController>
        [HttpPost]
        public int Post([FromBody] ItemsInProduction item)
        {
            int numEffected = item.Insert();
            return numEffected;
        }
    }
}
