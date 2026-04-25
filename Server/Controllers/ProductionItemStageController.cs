using Microsoft.AspNetCore.Mvc;
using Server.Models;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductionItemStageController : ControllerBase
    {
        [HttpPost("UpdateManualOrder")]
        public IActionResult UpdateManualOrder([FromBody] List<ManualPriorityUpdateRequest> updates)
        {
            if (updates == null) return BadRequest("Updates list is null");

            try
            {
                ProductionItemStage model = new ProductionItemStage();
                int result = model.UpdateAllManualPriorities(updates);
                return Ok(new { message = $"Updated {result} items" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
