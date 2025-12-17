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
using System.Linq;
using TPCI.DeckValidation;
using static SharedSDKUtils.DeckValidation.DeckValidationService;
using MatchLogic;

namespace Rainier.NativeOmukadeConnector.Patches
{
    [HarmonyPatch(typeof(DefaultDeckValidationController))]
    static class DeckValidationAllUseExpandedFormat
    {
        [HarmonyPrepare]
        static bool Prepare() => Plugin.Settings.ForceAllLegalityChecksToSucceed;

        [HarmonyPatch(typeof(DefaultDeckValidationController), nameof(DefaultDeckValidationController.IsCardValidForGameMode))]
        [HarmonyPrefix]

        static void IsCardValidForGameMode_Prefix(ref GameMode gameMode)
        {
            gameMode = GameMode.Expanded;
        }

        [HarmonyPatch(typeof(DefaultDeckValidationController), nameof(DefaultDeckValidationController.ValidateDeck))]
        [HarmonyPrefix]

        static void ValidateDeck_Prefix(ref GameMode gameMode)
        {
            gameMode = GameMode.Expanded;
        }

        [HarmonyPatch(typeof(DeckValidationManager), "ValidateDeckIgnoreUnowned")]
        [HarmonyPrefix]
        private static bool ValidateDeckIgnoreUnowned_Prefix(IDeckValidationController ____deckValidationController, DeckInfo deck, ref bool __result)
        {
            __result = ____deckValidationController.ValidateDeckIgnoreUnowned(deck, GameMode.Expanded);
            return false;
        }
    }

    [HarmonyPatch(typeof(DeckValidationService), nameof(DeckValidationService.ValidateDeck))]
    static class DeckValidationAlwaysIgnoreUnowned
    {
        static bool Prefix(ref DeckValidPackage __result, RulesFormat format, Dictionary<string, int> ownedCards, DeckInfo deck, IQueryableCardDatabase cardDatabase)
        {
            if (ownedCards.Count == 0)
            {
                return true;
            }
            __result = DeckValidationService.ValidateDeckIgnoreUnowned(format, deck, cardDatabase);
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
            __result.entries = __result.entries.Where((state) =>
            {
                return state.error != DeckValidState.Banned;

            }).ToArray();
        }
    }

    [HarmonyPatch(typeof(RulesFormat), nameof(RulesFormat.IsCardValidForFormat))]
    static class RulesFormat_UseImplementedExpandedListInsteadOfFormats
    {
        static bool Prepare() => Plugin.Settings.AskServerForImplementedCards;

        static bool Prefix(CardDataRow card, ref bool __result, DeckFormat ___format)
        {
            if (ClientPatches.ImplementedExpandedCardsFromServer == null)
            {
                Plugin.SharedLogger.LogWarning("[RulesFormat] Ask Server For Implemented Cards is enabled, but no data available when checking format.");
            }

            __result = ClientPatches.ImplementedExpandedCardsFromServer?.Contains(card.CardID) == true;
            return !__result;
        }
    }
}
