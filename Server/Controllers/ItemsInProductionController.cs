using Microsoft.AspNetCore.Mvc;
using Server.Models;
using static Server.Models.ProductionItemStage;

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

                List<ItemInProduction> sortedBoard = model.SortItemsByUrgency(board);

                return Ok(sortedBoard);
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
        public IActionResult InsertItem([FromBody] InsertItemInProductionRequest? itemData)
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
        public IActionResult UpdateStatus([FromBody] UpdateProductionStatusRequest? data)
        {
            try
            {
                ItemInProduction model = new ItemInProduction();
                int res = model.UpdateStatus(data);

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
