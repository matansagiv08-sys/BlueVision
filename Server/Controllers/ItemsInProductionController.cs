using Microsoft.AspNetCore.Mvc;
using Server.DAL;
using Server.Models;

namespace Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ItemsInProductionController : ControllerBase
    {

        [HttpGet("boardData")]
        public IActionResult GetBoardData()
        {
            try
            {
                ItemInProduction model = new ItemInProduction();
                List<ItemInProduction> board = model.GetBoardData();
                return Ok(board);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("GetInitialFormData")]
        public IActionResult GetInitialFormData()
        {
            try
            {
                DBservices dbs = new DBservices();
                var formData = new
                {
                    ProductionItems = dbs.GetProductionItems(),
                    Projects = dbs.GetProjects(), 
                    PlaneTypes = dbs.GetPlaneTypes(),
                    ExistingWorkOrders = dbs.GetUniqueWorkOrders(),
                    Priorities = dbs.GetPriorityLevels(),
                    Planes = dbs.GetPlanes()
                };
                return Ok(formData);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("InsertItem")]
        public IActionResult InsertItem([FromBody] System.Text.Json.Nodes.JsonObject itemData)
        {
            try
            {
                DBservices dbs = new DBservices();
                int numEffected = dbs.InsertItemInProduction(itemData);

                if (numEffected > 0) return Ok(new { message = "Success" });
                else return BadRequest(new { error = "Failed to insert" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
