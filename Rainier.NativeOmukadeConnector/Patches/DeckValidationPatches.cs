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
using MatchLogic;
using SharedLogicUtils.source.DeckValidation;
using SharedSDKUtils;
using SharedSDKUtils.DeckValidation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Emit;
using ThirdParty.Collections;
using TPCI.DeckValidation;
using static SharedSDKUtils.DeckValidation.DeckValidationService;

namespace Rainier.NativeOmukadeConnector.Patches
{
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

    [HarmonyPatch]
    static class DeckValidationIgnoreUnowned_OptionallyBypassAllChecks
    {
        static bool PlayButtonHanding = false;

        [HarmonyPrepare]
        static bool Prepare() => Plugin.Settings.ForceAllLegalityChecksToSucceed;

        [HarmonyPatch(typeof(DeckValidationService), nameof(DeckValidationService.ValidateDeckIgnoreUnowned))]
        [HarmonyPostfix]
        static void DeckValidationService_ValidateDeckIgnoreUnowned(ref DeckValidPackage __result)
        {
            __result.entries = __result.entries.Where((state) =>
            {
                return state.error != DeckValidState.Banned && state.error != DeckValidState.CardNotAllowedInFormat;
            }).ToArray();
        }

        [HarmonyPatch(typeof(DefaultDeckValidationController), nameof(DefaultDeckValidationController.IsCardValidForGameMode))]
        [HarmonyPrefix]

        static bool DefaultDeckValidationController_IsCardValidForGameMode(ref bool __result)
        {
            __result = true;
            return false;
        }

        [HarmonyPatch(typeof(DecksAlwaysInvalidValidator), nameof(DecksAlwaysInvalidValidator.ValidateDeck))]
        [HarmonyPatch(typeof(DecksAlwaysInvalidValidator), nameof(DecksAlwaysInvalidValidator.ValidateDeckIgnoreUnowned))]
        [HarmonyPrefix]
        static bool DecksAlwaysInvalidValidator_ValidateDeck(ref DeckValidPackage __result)
        {
            __result = new DeckValidPackage()
            {
                entries = []
            };
            return false;
        }

        [HarmonyPatch(typeof(HUBRankedController), nameof(HUBRankedController.Play))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> HUBRankedController_Play(IEnumerable<CodeInstruction> instructions)
        {
            return new CodeMatcher(instructions)
                .MatchForward(false,
                    new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(DeckInfo), nameof(DeckInfo.IsValid)))
                )
                .SetInstruction(
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(DeckValidationIgnoreUnowned_OptionallyBypassAllChecks), nameof(FakeIsValid)))
                )
                .InstructionEnumeration();
        }

        static bool FakeIsValid(DeckInfo instance, GameMode gameMode, ILogger optLogger = null)
        {
            return true;
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
