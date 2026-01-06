using AiAgentServiceLib.Agent;

namespace AiAgentServiceLib
{
    public class AiAgentsFabric
    {
        public Dictionary<string, AiAgent> Agents { get; set; }
        public AiAgentsFabric()
        {
            Agents = new Dictionary<string, AiAgent>();
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
            AiAgent aiAgent = new AiAgent(aiAgentName, apiKey, serverUrl, modelName, WelcomeMessage);
            Agents.Add(aiAgentName, aiAgent);
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

        public string GetAllAgentsName()
        {
            if (Agents.Count > 0)
            {
                var result = new List<string>();
                foreach (var item in Agents)
                {
                    result.Add(item.Key);
                }
                return string.Join("; ", result);
            }
            return "Список агентов пуст";
        }
    }
}
