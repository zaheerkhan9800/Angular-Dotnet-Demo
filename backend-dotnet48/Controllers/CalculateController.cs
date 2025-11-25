using System;
using System.Web.Http;
using BackendDotnet48.Models;

namespace BackendDotnet48.Controllers
{
    [RoutePrefix("calculate")]
    public class CalculateController : ApiController
    {
        [HttpPost]
        [Route("")]
        public IHttpActionResult Post([FromBody] CalcRequest req)
        {
            try
            {
                double ans;
                switch (req.Op)
                {
                    case "+":
                        ans = req.A + req.B;
                        break;
                    case "-":
                        ans = req.A - req.B;
                        break;
                    case "*":
                        ans = req.A * req.B;
                        break;
                    case "/":
                        if (req.B == 0)
                            throw new InvalidOperationException("Division by zero");
                        ans = req.A / req.B;
                        break;
                    default:
                        throw new InvalidOperationException("Unsupported operation");
                }
                return Ok(new { answer = ans });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }
}
