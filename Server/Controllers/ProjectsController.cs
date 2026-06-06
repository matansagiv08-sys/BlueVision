using Microsoft.AspNetCore.Mvc;
using Server.Models;

namespace Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProjectsController : ControllerBase
    {
        [HttpGet]
        public IEnumerable<Project> Get()
        {
            Project p = new Project();
            return p.GetProjects();
        }

        [HttpPost]
        public IActionResult Create([FromBody] CreateProjectRequest? data)
        {
            try
            {
                Project project = new Project();
                Project created = project.CreateProject(data);
                return Ok(created);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("full-status")]
        public ActionResult GetFullStatus()
        {
            try
            {
                Project project = new Project();
                List<Project> projects = project.GetFullProjectsStatus();

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
