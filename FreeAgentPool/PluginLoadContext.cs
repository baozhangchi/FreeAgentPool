#region File Header

// Solution: FreeAgentPool
// Project: FreeAgentPool
// FileName: PluginLoadContext.cs
// Create Time: 2022-09-01 16:01
// Update Time: 2022-09-02 8:49

#endregion

#region Namespaces

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

#endregion

namespace FreeAgentPool
{
    public class PluginLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public PluginLoadContext(string componentAssemblyPath) : base(true)
        {
            _resolver = new AssemblyDependencyResolver(componentAssemblyPath);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }

            return null;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (libraryPath != null)
            {
                return LoadUnmanagedDllFromPath(libraryPath);
            }

            return IntPtr.Zero;
        }
    }
}