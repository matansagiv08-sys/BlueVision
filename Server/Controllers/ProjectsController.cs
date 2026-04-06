using Microsoft.AspNetCore.Mvc;
using Server.DAL;
using Server.Models;

namespace Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProjectsController : ControllerBase
    {
        [HttpGet]
        [HttpGet]
        public IEnumerable<Project> Get()
        {
            Project p = new Project();
            return p.GetProjects();
        }
        [HttpGet("full-status")]
        public ActionResult GetFullStatus()
        {
            try
            {
                DBservices dbs = new DBservices();
                List<Project> projects = dbs.GetFullProjectsStatus();

                if (projects == null || projects.Count == 0)
                {
                    return NotFound("No projects found.");
                }

                return Ok(projects);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
}
