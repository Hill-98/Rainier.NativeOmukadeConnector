using HarmonyLib;
using TPCI.Rainier.Features.DeckEditor;

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

    [HarmonyPatch(typeof(CraftUnownedCardsButtonController))]
    static class CraftUnownedCardsButtonControllerPatches
    {
        [HarmonyPatch(nameof(CraftUnownedCardsButtonController.CanShowCraftAllButton))]
        [HarmonyPrefix]
        static bool CanShowCraftAllButton(ref bool __result)
        {
            __result = false;
            return false;
        }
    }
}