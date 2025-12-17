/*************************************************************************
* Rainier Native Omukade Connector
* (c) 2022 Hastwell/Electrosheep Networks 
* 
* This program is free software: you can redistribute it and/or modify
* it under the terms of the GNU Affero General Public License as published
* by the Free Software Foundation, either version 3 of the License, or
* (at your option) any later version.
* 
* This program is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
* GNU Affero General Public License for more details.
* 
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see <http://www.gnu.org/licenses/>.
**************************************************************************/

using BepInEx;
using BepInEx.Logging;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;

namespace Rainier.NativeOmukadeConnector
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("2aecaf59-9969-4ea5-b41c-b1ee114568fb", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        internal const string VERSION_STRING = "Native Omukade Connector \"NOC\" 2.2.1 (\"Auditioning Apple Rev1 Hill-98 mod\")";
        internal const string OMUKADE_VERSION = "Omukade Cheyenne-EX";

        internal static ManualLogSource SharedLogger;
        internal static ConfigurationSettings Settings;

        private void Awake()
        {
            SharedLogger = Logger;

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var exception = (Exception)e.ExceptionObject;
                Logger.LogError(exception.Message);
            };

            if (!Environment.GetCommandLineArgs().Contains("--enable-omukade"))
            {
                SharedLogger.LogWarning("Omukade not enabled by command-line; goodbye");
                return;
            }
            SharedLogger.LogWarning($"CMD Line Args is: {string.Join(" ", Environment.GetCommandLineArgs())}");
            SharedLogger.LogWarning($"CMD Line is: {Environment.CommandLine}");

            string config = Environment.GetEnvironmentVariable("OMUKADE_CONNECTOR_CONFIG") ?? "config-noc.json";

            if (File.Exists(config))
            {
                SharedLogger.LogMessage("Found config file");
                Settings = JsonConvert.DeserializeObject<ConfigurationSettings>(File.ReadAllText(config));
            }
            else
            {
                SharedLogger.LogWarning("No Config File Found! Using Dev defaults...");
                Settings = new ConfigurationSettings();
            }

            SharedLogger.LogMessage($"Omukade endpoint set to {Settings.OmukadeEndpoint}");


            // Plugin startup logic
            SharedLogger.LogInfo($"Performing patches for NOC...");

            try
            {
                new HarmonyLib.Harmony(nameof(Rainier.NativeOmukadeConnector)).PatchAll();
                SharedLogger.LogInfo($"Applied Patches");
            }
            catch (Exception ex)
            {
                SharedLogger.LogError(ex);
            }
        }
    }
}
