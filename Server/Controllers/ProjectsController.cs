using Microsoft.AspNetCore.Mvc;
using Server.DAL;
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
                // מחזיר שגיאה 500 עם פירוט במידה ומשהו השתבש בדרך
                return StatusCode(500, ex.Message);
            }
        }
    }
}
