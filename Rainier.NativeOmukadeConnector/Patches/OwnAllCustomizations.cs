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
using System.Reflection;

namespace Rainier.NativeOmukadeConnector.Patches
{
    [HarmonyPatch(typeof(AvatarCustomizationPlayerInventory))]
    internal static class OwnAllAvatarCustomizations
    {
        static bool Prepare(MethodBase original) => Plugin.Settings.EnableAllCosmetics;

        [HarmonyPatch(nameof(AvatarCustomizationPlayerInventory.IsOwned))]
        [HarmonyPrefix]
        static bool YouNowHaveAllTheOutfits(ref bool __result)
        {
            if (!Plugin.Settings.EnableAllCosmetics) return true;

            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(HUBDeckCustomizationController))]
    internal static class OwnAllDeckCustomizations
    {
        static bool Prepare(MethodBase original) => Plugin.Settings.EnableAllCosmetics;

        [HarmonyPatch("IsOwned")]
        [HarmonyPrefix]
        static bool IsOwned(ref bool __result)
        {
            if (!Plugin.Settings.EnableAllCosmetics) return true;

            __result = true;
            return false;
        }
    }
}