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
using ClientNetworking;
using ClientNetworking.Models.GameServer;
using System.Threading.Tasks;

namespace Rainier.NativeOmukadeConnector.Patches
{
    [HarmonyPatch(typeof(Client), nameof(Client.JoinGameAsync), typeof(JoinGameRequest), typeof(ResponseHandler<JoinGameResponse>), typeof(ErrorHandler))]
    static class Client_JoinGameAsync_GIGO
    {
        static bool Prefix(Client __instance, ref Task __result, JoinGameRequest request, ResponseHandler<JoinGameResponse> success, ErrorHandler failure)
        {
            //Plugin.SharedLogger.LogInfo("JoinGame is now returning ticket value to avoid HTTP call");
            success?.Invoke(__instance, new JoinGameResponse { gameId = request.ticket });

            __result = Task.CompletedTask;
            return false;
        }
    }
}
