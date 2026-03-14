using Microsoft.AspNetCore.Mvc;
using Server.Models;
// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PlanesController : ControllerBase
    {
        [HttpGet]
        public List<Plane> Get() => new Plane().GetPlanes();

        [HttpPost]
        public int Post([FromBody] Plane p) => p.Insert();

        [HttpPut]
        public int Put([FromBody] Plane p) => p.Update();

        [HttpDelete("{id}")]
        public int Delete(int id) => new Plane().Delete(id);
    }
}
