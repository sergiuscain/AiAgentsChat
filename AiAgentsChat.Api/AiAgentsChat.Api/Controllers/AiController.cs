using Microsoft.AspNetCore.Mvc;
using AiAgentServiceLib;

namespace AiAgentsChat.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AiController : ControllerBase
    {
        private AiAgentService _aiAgentService;
        public AiController(AiAgentService aiAgentService)
        {
            _aiAgentService = aiAgentService;
        }
        [HttpPost("PostApiPrompt")]
        public async Task<IActionResult> PostApiPrompt(string prompt)
        {
           
           var result = await _aiAgentService.PostPromptAsync(prompt);
            if (result != null) 
                return Ok(result);
            return BadRequest("Ошибка отправке запроса");
        }
    }
}
