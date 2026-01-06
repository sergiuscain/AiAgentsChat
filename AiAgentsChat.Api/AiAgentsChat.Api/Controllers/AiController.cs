using Microsoft.AspNetCore.Mvc;
using AiAgentServiceLib;

namespace AiAgentsChat.Api.Controllers;
[Route("api/[controller]")]
[ApiController]
public class AiController : ControllerBase
{
    private AiAgentsFabric _aiAgentsFabric;
    public AiController(AiAgentsFabric aiAgentsFabric)
    {
        _aiAgentsFabric = aiAgentsFabric;
    }
    [HttpPost("PostApiPrompt")]
    public async Task<IActionResult> PostApiPromptAsync(string prompt, string AiAgentName)
    {
           
        var result = await _aiAgentsFabric.PostPromptAsync(prompt, AiAgentName);
        if (result != null) 
            return Ok(result);
        return BadRequest("Ошибка отправке запроса");
    }
    [HttpPost("CreateAiAgent")]
    public bool CreateAgent(string AiAgentName)
    {
        return _aiAgentsFabric.CreateAgent(AiAgentName);
    }
}
