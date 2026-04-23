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
using RainierClientSDK.Inventory;

namespace Rainier.NativeOmukadeConnector.Patches
{
    [HarmonyPatch]
    internal static class ReferenceGetters
    {
        internal static PlatformCollectionService collectionServiceReference;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlatformCollectionService), nameof(PlatformCollectionService.GetCollectionsAsync))]
        static void Prefix(PlatformCollectionService __instance)
        {
            //Plugin.SharedLogger.LogInfo("Obtained the PlatformCollectionService reference!");
            collectionServiceReference = __instance;
        }
    }
}
