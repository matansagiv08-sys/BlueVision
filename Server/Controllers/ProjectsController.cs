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
        public List<Project> Get() => new Project().GetProjects();

        [HttpPost]
        public int Post([FromBody] Project p) => p.Insert();

        [HttpPut]
        public int Put([FromBody] Project p) => p.Update();

        [HttpDelete("{id}")]
        public int Delete(int id) => new Project().Delete(id);
    }
}
