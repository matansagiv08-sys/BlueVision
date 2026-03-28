using Microsoft.AspNetCore.Mvc;
using Server.Models;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProjectsController : ControllerBase
    {
        [HttpGet]
        // GET: api/Projects
        [HttpGet]
        public IEnumerable<Project> Get()
        {
            Project p = new Project();
            return p.GetProjects();
        }
    }
}
