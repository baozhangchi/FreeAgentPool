#region FileHeader

// Project:FreeAgent.Core
// File:IAgent.cs
// Creator:包张驰
// CreateTime:2022-07-19 10:37
// LastUpdateTime:2022-07-19 14:29

#endregion

namespace FreeAgent.Core
{
    public interface IAgent
    {
        //Task<AgentInfo> GetAgentsAsync();

        void Execute();
    }

    public class AgentInfo
    {
        public string Ip { get; set; }

        public int Port { get; set; }
    }
}