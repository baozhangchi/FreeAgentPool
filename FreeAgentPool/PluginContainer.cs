#region File Header

// Solution: FreeAgentPool
// Project: FreeAgentPool
// FileName: PluginContainer.cs
// Create Time: 2022-09-02 9:16
// Update Time: 2022-09-02 10:13

#endregion

#region Namespaces

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Timers;
using FreeAgent.Core;
using Timer = System.Timers.Timer;

#endregion

namespace FreeAgentPool;

public class PluginContainer
{
    private static PluginContainer? _instance;
    private static readonly object Lock = new();
    private readonly string _agentsFolder;
    private readonly string _tempFolder;
    private readonly Timer _timer;
    private readonly Dictionary<string, WeakReference> _weakReferenceMap = new();
    private List<string> _changedQueue = new();
    private readonly List<string> _commonDllFileNames;

    private PluginContainer()
    {
        _commonDllFileNames = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory).Where(x => x.EndsWith(".dll") || x.EndsWith(".exe") || x.EndsWith(".pdb")).Select(x => Path.GetFileName(x)).ToList();
        _agentsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "agents");
        if (!Directory.Exists(_agentsFolder))
        {
            Directory.CreateDirectory(_agentsFolder);
        }

        _tempFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".agents");
        if (Directory.Exists(_tempFolder))
        {
            Directory.Delete(_tempFolder, true);
        }

        Directory.CreateDirectory(_tempFolder);

        var directoryInfo = new DirectoryInfo(_tempFolder);
        if ((directoryInfo.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden)
        {
            directoryInfo.Attributes |= FileAttributes.Hidden;
        }


        ClearCommonDll(_agentsFolder);

        CopyFilesRecursively(_agentsFolder, _tempFolder);

        foreach (var directory in Directory.GetDirectories(_tempFolder))
        {
            var pluginName = Path.GetFileName(directory);
            LoadContext(pluginName, out var weakReference);
            _weakReferenceMap.Add(pluginName, weakReference);
        }

        var watcher = new FileSystemWatcher(_agentsFolder);
        watcher.NotifyFilter = NotifyFilters.CreationTime |
                               NotifyFilters.FileName |
                               NotifyFilters.DirectoryName |
                               NotifyFilters.Size;
        watcher.EnableRaisingEvents = true;
        watcher.IncludeSubdirectories = true;
        watcher.Created += Watcher_Created;
        watcher.Deleted += Watcher_Deleted;
        watcher.Changed += Watcher_Changed;
        watcher.Renamed += Watcher_Renamed;
        _timer = new Timer(3 * 1000);
        _timer.Elapsed += _timer_Elapsed;
    }

    private void ClearCommonDll(string targetFolder)
    {
        foreach (var dllFile in Directory.GetFiles(targetFolder, "*", SearchOption.AllDirectories).Where(x => x.EndsWith(".dll") || x.EndsWith(".exe") || x.EndsWith(".pdb")).ToList())
        {
            if (_commonDllFileNames.Contains(Path.GetFileName(dllFile)))
            {
                File.Delete(dllFile);
            }
        }
    }

    public static PluginContainer Default
    {
        get
        {
            if (_instance == null)
            {
                lock (Lock)
                {
                    _instance ??= new PluginContainer();
                }
            }

            return _instance;
        }
    }

    public ConcurrentDictionary<string, PluginLoadContext> ContextMap { get; } = new();

    private void _timer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        _timer.Stop();
        _changedQueue = _changedQueue.Distinct().ToList();
        foreach (var pluginName in _changedQueue)
        {
            WeakReference weakReference;
            UnloadContext(pluginName);
            if (_weakReferenceMap.ContainsKey(pluginName))
            {
                weakReference = _weakReferenceMap[pluginName];
                while (weakReference.IsAlive)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }

                _weakReferenceMap.Remove(pluginName);
            }

            var sourceFolder = Path.Combine(_agentsFolder, pluginName);
            var targetFolder = Path.Combine(_tempFolder, pluginName);
            if (Directory.Exists(targetFolder))
            {
                Directory.Delete(targetFolder, true);
            }

            if (!Directory.GetFiles(sourceFolder, "*", SearchOption.AllDirectories)
                .Any(x => x.EndsWith(".dll") || x.EndsWith("*.exe")))
            {
                continue;
            }
            CopyFilesRecursively(sourceFolder, targetFolder);
            ClearCommonDll(targetFolder);
            LoadContext(pluginName, out weakReference);
            if (_weakReferenceMap.ContainsKey(pluginName))
            {
                _weakReferenceMap[pluginName] = weakReference;
            }
            else
            {
                _weakReferenceMap.Add(pluginName, weakReference);
            }
        }
    }

    private void Watcher_Renamed(object sender, RenamedEventArgs e)
    {
    }

    private void Watcher_Changed(object sender, FileSystemEventArgs e)
    {
        var path = e.Name;
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var pluginName = path.Contains(Path.DirectorySeparatorChar)
            ? path.Substring(0, path.IndexOf(Path.DirectorySeparatorChar))
            : path;
        _changedQueue.Add(pluginName);
        _timer.Stop();
        _timer.Start();
    }

    private void Watcher_Deleted(object sender, FileSystemEventArgs e)
    {
        var path = e.Name;
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var pluginName = path.Contains(Path.DirectorySeparatorChar)
            ? path.Substring(0, path.IndexOf(Path.DirectorySeparatorChar))
            : path;
        _changedQueue.Add(pluginName);
        _timer.Stop();
        _timer.Start();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void LoadContext(string pluginName, out WeakReference weakReference)
    {
        var pluginFolder = Path.Combine(_tempFolder, pluginName);
        var context = new PluginLoadContext(pluginFolder);
        weakReference = new WeakReference(context);
        ContextMap.TryAdd(pluginFolder, context);
        foreach (var path in Directory.GetFiles(pluginFolder, "*.dll", SearchOption.AllDirectories)
                     .Union(Directory.GetFiles(pluginFolder, "*.exe", SearchOption.AllDirectories).ToList()))
        {
            context.LoadFromAssemblyPath(path);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void UnloadContext(string pluginName)
    {
        var pluginFolder = Path.Combine(_tempFolder, pluginName);
        if (ContextMap.TryRemove(pluginFolder, out var context))
        {
            context.Unload();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Execute()
    {
        foreach (var pluginLoadContext in ContextMap.Values)
        {
            var types = pluginLoadContext.Assemblies.SelectMany(x => x.ExportedTypes).ToList();
            types = types.Where(x => typeof(IAgent).IsAssignableFrom(x)).ToList();
            foreach (var type in types)
            {
                var obj = Activator.CreateInstance(type) as IAgent;
                obj?.Execute();
            }
        }
    }

    private void Watcher_Created(object sender, FileSystemEventArgs e)
    {
        var path = e.Name;
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (_commonDllFileNames.Contains(Path.GetFileName(e.FullPath)))
        {
            File.Delete(e.FullPath);
            return;
        }

        var pluginName = path.Contains(Path.DirectorySeparatorChar)
            ? path.Substring(0, path.IndexOf(Path.DirectorySeparatorChar))
            : path;
        _changedQueue.Add(pluginName);
        _timer.Stop();
        _timer.Start();
    }

    private static void CopyFilesRecursively(string sourcePath, string targetPath)
    {
        if (!Directory.Exists(targetPath))
        {
            Directory.CreateDirectory(targetPath);
        }

        //创建所有新目录
        foreach (var dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));
        }

        //复制所有文件 & 保持文件名和路径一致
        foreach (var newPath in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
        {
            File.Copy(newPath, newPath.Replace(sourcePath, targetPath), true);
        }
    }
}