using Microsoft.AspNetCore.Mvc;
using Server.Models;

namespace Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PlanesController : ControllerBase
    {
        [HttpPost]
        public IActionResult Create([FromBody] CreatePlaneRequest? data)
        {
            try
            {
                Plane plane = new Plane();
                object created = plane.CreatePlane(data);
                return Ok(created);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
