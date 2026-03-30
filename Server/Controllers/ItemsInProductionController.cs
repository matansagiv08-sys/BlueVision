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
    }
}
