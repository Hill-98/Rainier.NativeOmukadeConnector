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
using Omukade.Cheyenne.CustomMessages;
using ClientNetworking;
using ClientNetworking.Codecs;
using Rainier.NativeOmukadeConnector.Messages;

namespace Rainier.NativeOmukadeConnector.Patches
{
    [HarmonyPatch(typeof(Command<GetOnlineFriends>))]
    [HarmonyPatch(typeof(Command<GetRankData>))]
    [HarmonyPatch(typeof(Command<SupplementalDataMessageV2>))]
    internal static class CommandPatches
    {
#warning TODO; add a custom payload for SDMs and GOFs to leverage Rainier's native encoding; this hacky patch runs on every message sent
        [HarmonyPatch(nameof(Command<SupplementalDataMessageV2>.Encode))]
        [HarmonyPrefix]
        static void ForceJsonForOmukadePayloads(ref ICodec serializer, object body)
        {
            if(body is SupplementalDataMessageV2 || body is GetOnlineFriends || body is GetRankData || body is GetImplementedExpandedCardsV1)
            {
                Plugin.SharedLogger.LogDebug($"Forcing {body.GetType().Name} to be processed as JSON");
                serializer = WswCommon.JsonCodec;
            }
        }
    }
}
