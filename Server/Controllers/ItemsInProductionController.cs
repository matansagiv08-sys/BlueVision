using Microsoft.AspNetCore.Mvc;
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
                ItemInProduction model = new ItemInProduction();
                object formData = model.GetInitialFormData();
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
                ItemInProduction model = new ItemInProduction();
                int numEffected = model.InsertItem(itemData);

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
                bool resetFuture = data["ResetFuture"]?.GetValue<bool>() ?? false;

                DateTime? userTime = null;
                if (data["UserTime"] != null)
                {
                    userTime = DateTime.Parse(data["UserTime"].ToString());
                }

                ItemInProduction model = new ItemInProduction();
                int res = model.UpdateStatus(serial, itemID, stageID, statusID, comment, userTime, resetFuture);

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
