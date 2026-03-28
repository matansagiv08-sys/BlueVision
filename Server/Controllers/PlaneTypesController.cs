using Microsoft.AspNetCore.Mvc;
using Server.Models;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PlaneTypesController : ControllerBase
    {
        // GET: api/PlaneTypes
        [HttpGet]
        public IEnumerable<PlaneType> Get()
        {
            PlaneType pt = new PlaneType();
            return pt.GetPlaneTypes();
        }

        // POST: api/PlaneTypes
        [HttpPost]
        public int Post([FromBody] PlaneType pt) => pt.Insert();

        // DELETE: api/PlaneTypes/5
        [HttpDelete("{id}")]
        public int Delete(int id) => new PlaneType().Delete(id);
    }
}
