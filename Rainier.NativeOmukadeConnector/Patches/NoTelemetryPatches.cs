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
using UnityEngine;

namespace Rainier.NativeOmukadeConnector.Patches
{
    [HarmonyPatch(typeof(Endpoint_ProdPipeline))]
    internal static class NoTelemetryPatches
    {
        // Token: 0x06000040 RID: 64 RVA: 0x00002D74 File Offset: 0x00000F74
        [HarmonyPatch("SendEvent")]
        [HarmonyPrefix]
        private static bool SendEventNowSendsNoTelemetry(string eventName)
        {
            Debug.Log("[NoTelemetry] Swallowed telemetry event " + eventName);
            return false;
        }
    }
}
