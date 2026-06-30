using _Rainier.Scripts.UI.DeckEditor.Collection.OwnedCardQuantityProviders;
using HarmonyLib;

namespace Rainier.NativeOmukadeConnector.Patches
{
    [HarmonyPatch]
    static class IArchetypeOwnedCardQuantityProviderPatches
    {
        [HarmonyPatch(typeof(ArchetypeIdDictionaryCountProvider), nameof(ArchetypeIdDictionaryCountProvider.GetOwnedQuantityForArchetypeOrZero))]
        [HarmonyPrefix]
        static bool GetOwnedQuantityForArchetypeOrZero(ref int __result)
        {
            __result = 60;
            return false;
        }
    }

    [HarmonyPatch]
    static class IOwnedCardQuantityProviderPatches
    {
        [HarmonyPatch(typeof(CardIdDictionaryCountProvider), nameof(CardIdDictionaryCountProvider.GetOwnedQuantityForCardOrZero))]
        [HarmonyPatch(typeof(CardIdDictionaryCountProvider), nameof(SimpleCardCountProvider.GetOwnedQuantityForCardOrZero))]    
        [HarmonyPrefix]
        static bool GetOwnedQuantityForCardOrZero(ref int __result)
        {
            __result = 60;
            return false;
        }
    }
}