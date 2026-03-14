using Microsoft.AspNetCore.Mvc;
using Server.Models;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ItemsInProductionController : ControllerBase
    {
        // GET: api/ItemsInProduction
        // מחזיר את כל הרשימה מה-DB ללקוח (React)
        [HttpGet]
        public IEnumerable<ItemInProduction> Get()
        {
            ItemInProduction item = new ItemInProduction();
            return item.Read();
        }

        [HttpPost]
        public int Post([FromBody] ItemInProduction item)
        {
            return item.Insert();
        }

        [HttpPut]
        public int Put([FromBody] ItemInProduction item)
        {
            return item.Update();
        }

        [HttpDelete("serial/{serialNumber}/product/{productItemID}")]
        public int Delete(int serialNumber, string productItemID)
        {
            ItemInProduction item = new ItemInProduction();
            item.SerialNumber = serialNumber;
            item.ProductItemID = productItemID;
            return item.Delete();
        }
    }
}
