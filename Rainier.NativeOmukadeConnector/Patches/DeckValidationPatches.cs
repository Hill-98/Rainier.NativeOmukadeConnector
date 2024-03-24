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

using CardDatabase.DataAccess;
using HarmonyLib;
using SharedLogicUtils.Config;
using SharedSDKUtils.DeckValidation;
using SharedSDKUtils;
using System;
using System.Collections.Generic;
using System.Text;
using static SharedSDKUtils.DeckValidation.DeckValidationService;

namespace Rainier.NativeOmukadeConnector.Patches
{
    [HarmonyPatch(typeof(DeckValidationService), nameof(DeckValidationService.ValidateDeck), typeof(RulesFormat), typeof(Dictionary<string, int>), typeof(DeckInfo), typeof(IQueryableCardDatabase), typeof(IConfigLoader))]
    static class DeckValidationAlwaysIgnoreUnowned
    {
        static bool Prefix(ref DeckValidPackage __result, RulesFormat format, DeckInfo deck, IQueryableCardDatabase cardDatabase, IConfigLoader configLoader)
        {
            __result = DeckValidationService.ValidateDeckIgnoreUnowned(format, deck, cardDatabase, configLoader);
            return false;
        }
    }

    [HarmonyPatch(typeof(DeckValidationService), nameof(DeckValidationService.ValidateDeckIgnoreUnowned))]
    static class DeckValidationIgnoreUnowned_OptionallyBypassAllChecks
    {
        [HarmonyPrepare]
        static bool Prepare() => Plugin.Settings.ForceAllLegalityChecksToSucceed;

        static void Postfix(ref DeckValidPackage __result)
        {
            if (Plugin.Settings.ForceAllLegalityChecksToSucceed)
            {
                __result.entries = __result.entries.Where((state) => state.error != DeckValidState.Banned || ClientPatches.ImplementedExpandedCardsFromServer.Contains(state.cardID)).ToArray();
            }
        }
    }

    [HarmonyPatch(typeof(RulesFormat), nameof(RulesFormat.IsCardValidForFormat))]
    static class RulesFormat_UseImplementedExpandedListInsteadOfFormats
    {
        static bool Prepare() => Plugin.Settings.AskServerForImplementedCards;

        static bool Prefix(CardDataRow card, ref bool __result, DeckFormat ___format)
        {
            // Only consider Expanded for this patch; don't perform this validation for other formats, or they will appear legal.
            // This implementation makes all possible cards that aren't banned legal.
            if( ___format != DeckFormat.EXPANDED)
            {
                return true;
            }

            // Don't process if no Implemented Cards data is available; warn about this instead.
            if(ClientPatches.ImplementedExpandedCardsFromServer == null)
            {
                Plugin.SharedLogger.LogWarning("[RulesFormat] Ask Server For Implemented Cards is enabled, but no data available when checking format.");
                return true;
            }

            // If card is implemented serverside, always declare it valid for Expanded. It could still be banned (eg. Shaymin EX, Lysandre's Trump Card)
            if(ClientPatches.ImplementedExpandedCardsFromServer.Contains(card.CardID))
            {
                __result = true;
                return false;
            }

            return true;
        }
    }
}
