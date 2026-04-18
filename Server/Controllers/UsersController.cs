using Microsoft.AspNetCore.Mvc;
using Server.Models;

namespace Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetAllUsers()
        {
            try
            {
                UserAccount model = new UserAccount();
                List<AppUser> users = model.GetAllUsers();
                return Ok(users);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public IActionResult GetUserByID(int id)
        {
            try
            {
                UserAccount model = new UserAccount();
                AppUser? user = model.GetUserByID(id);
                if (user == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                return Ok(user);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult CreateUser([FromBody] AppUser? user)
        {
            try
            {
                UserAccount model = new UserAccount();
                bool success = model.CreateUser(user, out string message, out int userID, out string temporaryPassword);
                object response = new
                {
                    success,
                    message,
                    userID,
                    temporaryPassword
                };

                if (success)
                {
                    return Ok(response);
                }

                return BadRequest(response);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPut("access")]
        public IActionResult UpdateUserAccess([FromBody] AppUser? user)
        {
            try
            {
                UserAccount model = new UserAccount();
                bool updated = model.UpdateUserAccess(user);
                if (updated)
                {
                    return Ok(new { message = "User access updated successfully" });
                }

                return BadRequest(new { message = "User access update failed" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public IActionResult UpdateUser(int id, [FromBody] AppUser? user)
        {
            try
            {
                if (user == null)
                {
                    return BadRequest(new { message = "Invalid request" });
                }

                user.UserID = id;
                UserAccount model = new UserAccount();
                bool updated = model.UpdateUser(user);
                if (updated)
                {
                    return Ok(new { message = "User updated successfully" });
                }

                return BadRequest(new { message = "User update failed" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("reset-password")]
        public IActionResult ResetPassword([FromBody] AppUser? user)
        {
            try
            {
                UserAccount model = new UserAccount();
                int userID = user?.UserID ?? 0;
                bool success = model.ResetPassword(userID, out string message, out string temporaryPassword);
                object response = new
                {
                    success,
                    message,
                    temporaryPassword
                };

                if (success)
                {
                    return Ok(response);
                }

                return BadRequest(response);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public IActionResult DeleteUser(int id, [FromQuery] int currentUserID = 0)
        {
            try
            {
                UserAccount model = new UserAccount();
                bool success = model.DeleteUser(id, currentUserID, out string message);
                object response = new
                {
                    success,
                    message
                };

                if (success)
                {
                    return Ok(response);
                }

                return BadRequest(response);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
