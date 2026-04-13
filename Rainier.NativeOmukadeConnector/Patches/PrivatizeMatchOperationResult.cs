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
using MatchLogic;
using RainierClientSDK;
using System.Collections.Generic;

namespace Rainier.NativeOmukadeConnector.Patches
{
    [HarmonyPatch(typeof(MatchInfo))]
    internal class PrivatizeMatchOperationResult
    {
        [HarmonyPatch("UpdateWithResults")]
        [HarmonyPrefix]
        internal static void Privatize(ref MatchOperationResult result, MatchInfo __instance)
        {
            // copied from 1.23.1 client
            MatchBoard boardState = __instance.GetBoardState();
            if (boardState == null || boardState.player1 == null || boardState.player2 == null)
            {
                return;
            }
            List<MatchEntity> list = new List<MatchEntity>();
            List<string> list2 = new List<string>();
            bool flag = boardState.player1.GetMetaData<int>(MetaDataKey.PlayerSetupState, -1, true) == 0;
            bool flag2 = boardState.player2.GetMetaData<int>(MetaDataKey.PlayerSetupState, -1, true) == 0;
            foreach (ActionModification actionModification in result.actionModifications)
            {
                actionModification.Privatize(flag, flag2, __instance.isPlayer1, list, list2);
            }
        }
    }
}
