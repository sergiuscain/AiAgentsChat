using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AiAgentServiceLib
{
    public class AiAgentsFabric
    {
        public Dictionary<string, AiAgentService> Agents { get; set; }
        public AiAgentsFabric()
        {
            Agents = new Dictionary<string, AiAgentService>();
        }
        /// <summary>
        /// Создает новый экземпляр AI-Агента с уникальным именем.
        /// </summary>
        /// <param name="name">Уникальное имя Агента и его ID</param>
        /// <param name="apiKey"></param>
        /// <param name="serverUrl"></param>
        /// <param name="modelName"></param>
        /// <param name="WelcomeMessage"></param>
        /// <returns></returns>
        public bool CreateAgent(string aiAgentName, string apiKey = Constants.ApiKey, string serverUrl = Constants.ServerUrl, string modelName = Constants.ModelName, string WelcomeMessage = Constants.StartPrompt)
        {
            if (Agents.GetValueOrDefault(aiAgentName) != null)
                return false;
            AiAgentService aiAgentService = new AiAgentService(aiAgentName, apiKey, serverUrl, modelName, WelcomeMessage);
            Agents.Add(aiAgentName, aiAgentService);
            return true;
        }
        public async Task<string> PostPromptAsync(string prompt, string aiAgentName)
        {
            var agent = Agents.GetValueOrDefault(aiAgentName); // .NET Core 2.0+
            if (agent != null)
            {
                return await agent.PostPromptAsync(prompt);
            }
            return "Агента с таким именем не существует";
        }
    }
}
