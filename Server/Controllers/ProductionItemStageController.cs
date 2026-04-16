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
        public IActionResult UpdateManualOrder([FromBody] List<dynamic> updates)
        {
            try
            {
                // יצירת מופע של המודל שבו הגדרת את הפונקציה
                ProductionItemStage model = new ProductionItemStage();

                // הפעלת הלוגיקה העסקית שנמצאת במודל
                int result = model.UpdateAllManualPriorities(updates);

                if (result > 0)
                {
                    return Ok(new { message = $"Successfully updated {result} items" });
                }
                return BadRequest("No records were updated.");
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
