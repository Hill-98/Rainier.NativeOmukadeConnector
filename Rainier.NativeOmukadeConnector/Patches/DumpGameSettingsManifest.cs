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

using HarmonyLib;
using System.IO;

namespace Rainier.NativeOmukadeConnector.Patches
{
    [HarmonyPatch(typeof(GameSettingsEndpoint), "Initialize")]
    internal static class DumpGameSettingsManifest
    {
        [HarmonyPrepare]
        static bool Prepare() => Plugin.Settings.DumpManifestFileUrl;

        [HarmonyPrefix]
        static void Prefix()
        {
            using StreamWriter writer = new StreamWriter("rnoc-game-settings-endpoints.log");
            writer.WriteLine("GSES.GetConfigPath: " + GameSettingsEndpointSettings.instance.GetConfigPath());

            writer.Close();
        }
    }
}
