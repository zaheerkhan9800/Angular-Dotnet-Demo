using Microsoft.AspNetCore.Mvc;
using BackendDotnet.Models;

namespace BackendDotnet.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class CalculateController : ControllerBase
    {
        [HttpPost]
        public IActionResult Post([FromBody] CalcRequest req)
        {
            try
            {
                double ans = req.Op switch
                {
                    "+" => req.A + req.B,
                    "-" => req.A - req.B,
                    "*" => req.A * req.B,
                    "/" => req.B == 0 ? throw new InvalidOperationException("Division by zero") : req.A / req.B,
                    _ => throw new InvalidOperationException("Unsupported operation")
                };
                return Ok(new { answer = ans });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { detail = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { detail = ex.Message });
            }
        }
    }
}
