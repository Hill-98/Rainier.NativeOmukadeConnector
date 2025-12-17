using HarmonyLib;
using Newtonsoft.Json;
using SharedSDKUtils.DeckValidation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace Rainier.NativeOmukadeConnector.Patches
{
    [HarmonyPatch(typeof(ArchetypeDBCards))]
    static class ArchetypeDBCardsPatches
    {
        [HarmonyPatch(nameof(ArchetypeDBCards.CardNumber))]
        [HarmonyPrefix]
        static bool QuantityForCard(ref int __result)
        {
            __result = 60;
            return false;
        }

        [HarmonyPatch(nameof(ArchetypeDBCards.TotalOwnedQuantity))]
        [HarmonyPrefix]
        static bool TotalOwnedQuantity(ref int __result)
        {
            __result = 60;
            return false;
        }

        [HarmonyPatch(nameof(ArchetypeDBCards.TotalOwnedQuantityForSpecificCard))]
        [HarmonyPrefix]
        static bool TotalOwnedQuantityForSpecificCard(ref int __result)
        {
            __result = 60;
            return false;
        }
    }
}
