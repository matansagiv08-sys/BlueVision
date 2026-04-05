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

        [HttpPut("updateStatus")]
        public IActionResult UpdateStatus([FromBody] System.Text.Json.Nodes.JsonObject data)
        {
            try
            {
                int serial = data["SerialNumber"]?.GetValue<int>() ?? 0;
                string itemID = data["ProductionItemID"]?.ToString();
                int stageID = data["ProductionStageID"]?.GetValue<int>() ?? 0;
                int statusID = data["ProductionStatusID"]?.GetValue<int>() ?? 0;
                string comment = data["Comment"]?.ToString();

                DateTime? userTime = null;
                if (data["UserTime"] != null)
                {
                    userTime = DateTime.Parse(data["UserTime"].ToString());
                }

                DBservices dbs = new DBservices();
                int res = dbs.UpdateStageStatus(serial, itemID, stageID, statusID, comment, userTime);

                if (res > 0) return Ok(new { message = "Status updated successfully" });
                return BadRequest("Could not update status");
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
