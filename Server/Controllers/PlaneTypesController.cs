using Microsoft.AspNetCore.Mvc;
using Server.Models;


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
    }
}
