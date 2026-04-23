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
using TMPro;
using TPCI.Build;

namespace Rainier.NativeOmukadeConnector.Patches
{
    [HarmonyPatch(typeof(StartupScreenText))]
    static class VersionStringPatches
    {
        [HarmonyPatch(nameof(StartupScreenText.SetVersionTextWithUser))]
        [HarmonyPrefix]
        static bool SetVersionTextWithUser(string user, TextMeshProUGUI ___versionText)
        {
            ___versionText.SetText(user + "\n" + GameVersionInfo.instance.GetGameVersion() + "\n" + GetOmukadeString());
            return false;
        }

        [HarmonyPatch(nameof(StartupScreenText.SetVersionText))]
        [HarmonyPrefix]
        static bool SetVersionText(TextMeshProUGUI ___versionText)
        {
            ___versionText.SetText(GameVersionInfo.instance.GetGameVersion() + "\n" + GetOmukadeString());
            return false;
        }

        static string GetOmukadeString() => $"{Plugin.VERSION_STRING}\n{Plugin.OMUKADE_VERSION} - {Plugin.Settings.OmukadeEndpoint}";
    }
}
