using FreeAgent.Core;

namespace BeesProxyAgent
{
    public class Agent : IAgent
    {
        //public Task<AgentInfo> GetAgentsAsync()
        //{
        //    throw new NotImplementedException();
        //}

        public void Execute()
        {
            Console.WriteLine($"BeesProxyAgent");
        }
    }
}