using System;
using System.Collections.Generic;
using BepInEx;
using System.IO;
using System.Reflection;

namespace LiveModReloader
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.zorkats.livemodreloader";
        public const string PluginName = "Live Mod Reloader";
        public const string PluginVersion = "0.1.0";


        private const string TargetTestPlugin = "CustomCrest.dll";

        // NEW: A class-level field to hold our watcher instance so it doesn't get garbage collected.
        private FileSystemWatcher _watcher;

        // NEW: A dictionary to track the plugins we are managing and the AppDomain they live in.
        private readonly Dictionary<string, AppDomain> _managedPlugins = new Dictionary<string, AppDomain>();

        private void Awake()
        {
            Logger.LogInfo($"Plugin {PluginName} v{PluginVersion} is loaded!");
            InitializeWatcher();

            // NEW: On startup, we'll manually load our target test plugin.
            var pluginPath = Path.Combine(Paths.PluginPath, TargetTestPlugin);
            if (File.Exists(pluginPath))
            {
                LoadPlugin(pluginPath);
            }
        }
        // NEW: A dedicated method for all watcher setup logic.
        private void InitializeWatcher()
        {
            // We use BepInEx's built-in Paths.PluginPath to reliably get the plugins folder.
            _watcher = new FileSystemWatcher(Paths.PluginPath)
            {
                // We want to be notified when a file is written to.
                NotifyFilter = NotifyFilters.LastWrite,

                // We only care about .dll files.
                Filter = "*.dll",

                // We should also watch for changes in any subfolders.
                IncludeSubdirectories = true,

                // This starts the watcher.
                EnableRaisingEvents = true
            };

            // This line connects the 'Changed' event of the watcher to our custom method.
            // Now, whenever a file changes, OnPluginChanged will be called.
            _watcher.Changed += OnPluginChanged;

            Logger.LogInfo("File watcher initialized. Monitoring for plugin changes.");
        }

        // NEW: This is the 'event handler' method. It runs when the watcher detects a change.
        private void LoadPlugin(string dllPath)
        {
            var dllName = Path.GetFileName(dllPath);
            if (_managedPlugins.ContainsKey(dllName))
            {
                Logger.LogWarning($"Plugin {dllName} is already loaded.");
                return;
            }

            Logger.LogInfo($"Loading {dllName} into a new AppDomain...");

            // Create a new AppDomain with a unique name.
            var domain = AppDomain.CreateDomain($"{dllName}_Domain_{DateTime.Now.Ticks}");

            // Load the assembly into the new domain.
            // Note: This just loads the code, it doesn't run the plugin's Awake() yet.
            domain.Load(AssemblyName.GetAssemblyName(dllPath));

            // Store the AppDomain so we can unload it later.
            _managedPlugins[dllName] = domain;
            Logger.LogInfo($"{dllName} has been loaded successfully.");
        }

        // NEW: Method to unload a plugin by destroying its AppDomain. THIS IS THE KEY!
        private void UnloadPlugin(string dllName)
        {
            if (!_managedPlugins.TryGetValue(dllName, out var domain))
            {
                Logger.LogWarning($"Plugin {dllName} is not being managed, cannot unload.");
                return;
            }

            Logger.LogInfo($"Unloading AppDomain for {dllName}...");

            // Unload the entire AppDomain. This releases the file lock.
            AppDomain.Unload(domain);
            _managedPlugins.Remove(dllName);

            Logger.LogInfo($"AppDomain for {dllName} unloaded. File lock should be released.");
        }

        private void OnPluginChanged(object sender, FileSystemEventArgs e)
        {
            // UPDATED: Now, when a change is detected, we call our unload logic.
            // We only care about changes to our target plugin for this test.
            if (e.Name.Equals(TargetTestPlugin, StringComparison.OrdinalIgnoreCase))
            {
                UnloadPlugin(e.Name);
            }
        }
    }
}