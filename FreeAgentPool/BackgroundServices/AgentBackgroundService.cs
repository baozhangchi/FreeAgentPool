using System.Composition.Hosting;
using FreeAgent.Core;
using Timer = System.Timers.Timer;

namespace FreeAgentPool.BackgroundServices
{
    public class AgentBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _provider;

        public AgentBackgroundService(IServiceProvider provider)
        {
            _provider = provider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Timer timer = new Timer(5 * 1000);
            timer.Elapsed += (s, e) =>
            {
                try
                {
                    timer.Enabled = false;
                    PluginContainer.Default.Execute();
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                }
                finally
                {
                    timer.Enabled = true;
                }
            };
            timer.Start();
        }

        //private async Task UpdateAgentsAsync(IAgent agent)
        //{
        //    var agents = await agent.GetAgentsAsync();
        //}
    }
}
