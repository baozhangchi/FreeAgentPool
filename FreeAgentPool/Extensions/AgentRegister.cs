using System.Composition.Convention;
using System.Composition.Hosting;
using System.Runtime.Loader;
using FreeAgent.Core;

namespace FreeAgentPool.Extensions
{
    public class AgentRegister
    {
        static AgentRegister()
        {
            var assemblies = Directory.GetFiles(AppContext.BaseDirectory, "*.dll", SearchOption.TopDirectoryOnly)
                .Select(AssemblyLoadContext.Default.LoadFromAssemblyPath).ToList();
            var conventions = new ConventionBuilder();
            conventions.ForTypesDerivedFrom<IAgent>().Export<IAgent>().Shared();
            Container = new ContainerConfiguration().WithAssemblies(assemblies, conventions).CreateContainer();
        }

        public static CompositionHost Container { get; set; }
    }

    public static class AgentExtensions
    {
        public static IServiceCollection RegisterAgent(this IServiceCollection service)
        {
            return service.AddSingleton(AgentRegister.Container);
        }
    }
}
