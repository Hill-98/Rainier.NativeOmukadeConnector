using ClientNetworking;
using HarmonyLib;
using RainierClientSDK;
using RainierClientSDK.source.Friend.Implementations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using TMPro;

namespace Rainier.NativeOmukadeConnector.Patches
{
    [HarmonyPatch]
    public static class GameManager_InitializeManagerPatches
    {
        static bool Prepare() => Plugin.Settings.ShowManagerLoadingStatus;

        // GameManager.<InitializeManagers>d__35
        // This patches one of the async method fragments for GameManager.InitializeManagers
        // I didn't want to hard-code it to a specific method name, as the name may be unstable from build to build.
        static IEnumerable<MethodBase> TargetMethods() => typeof(GameManager)
            .GetNestedTypes(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(nt => nt.Name.StartsWith($"<InitializeManagers>"))
            .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic))
            .Where(m => m.Name == "MoveNext")
            .Take(1);

        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo updateLoadStringMethod = typeof(StartupScreenText).GetMethod(nameof(StartupScreenText.UpdateLoadString));
            MethodInfo renderManagersList = typeof(GameManager_InitializeManagerPatches).GetMethod(nameof(GameManager_InitializeManagerPatches.RenderInitializingManagers));

            FieldInfo inititializedManagersListField = typeof(GameManager).GetField("initializedManagersList", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo loadingManagersListField = typeof(GameManager).GetField("loadingManagersList", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo notLoadedManagersListField = typeof(GameManager).GetField("unloadedManagersList", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo gameManagerSST = typeof(GameManager).GetField(nameof(GameManager.startupScreenText));
            foreach (CodeInstruction instr in instructions)
            {
                if(instr.Calls(updateLoadStringMethod))
                {
                    yield return instr;
                    yield return new CodeInstruction(OpCodes.Ldloc_1);
                    yield return new CodeInstruction(OpCodes.Ldfld, gameManagerSST);
                    yield return new CodeInstruction(OpCodes.Ldloc_1);
                    yield return new CodeInstruction(OpCodes.Ldfld, notLoadedManagersListField);
                    yield return new CodeInstruction(OpCodes.Ldloc_1);
                    yield return new CodeInstruction(OpCodes.Ldfld, loadingManagersListField);
                    yield return new CodeInstruction(OpCodes.Ldloc_1);
                    yield return new CodeInstruction(OpCodes.Ldfld, inititializedManagersListField);
                    yield return new CodeInstruction(OpCodes.Call, renderManagersList);
                }
                else
                {
                    yield return instr;
                }
            }
        }

        public static void RenderInitializingManagers(StartupScreenText sst, List<ManagerWithErrorCode> notLoadedManagers, List<ManagerWithErrorCode> loadingManagers, List<ManagerWithErrorCode> initializedManagers)
        {
            const string CLONE_SUFFIX = "(Clone)";
            string transformManagerTupleToName(ManagerWithErrorCode manager) => manager.ManagerObject.name.Replace(CLONE_SUFFIX, string.Empty);

            string textToDisplay = $"NOT LOADED ({notLoadedManagers.Count}) :: {string.Join(" - ", notLoadedManagers.Select(transformManagerTupleToName))}\nLOADING ({loadingManagers.Count}) :: {string.Join(" - ", loadingManagers.Select(transformManagerTupleToName))}\nLOADED ({initializedManagers.Count}) :: {string.Join(" - ", initializedManagers.Select(transformManagerTupleToName))}\n";

            TextMeshProUGUI textThingee = (TextMeshProUGUI) AccessTools.Field(typeof(StartupScreenText), "loadingText").GetValue(sst);
            textThingee.text = textToDisplay;
        }
    }

    // Token: 0x0200001C RID: 28
    [HarmonyPatch(typeof(PlatformRainierClient))]
    internal static class PlatformRainierClient_DisableMultipleReconnections
    {
        // Token: 0x0600003D RID: 61 RVA: 0x00002D2D File Offset: 0x00000F2D
        [HarmonyPatch("Setup")]
        [HarmonyPrefix]
        private static bool Setup_Prefix()
        {
            return !PlatformRainierClient_DisableMultipleReconnections.AlreadyConnected;
        }

        // Token: 0x0600003E RID: 62 RVA: 0x00002D37 File Offset: 0x00000F37
        [HarmonyPatch("OnNetworkChange")]
        [HarmonyPrefix]
        private static bool OnNetworkChange_Prefix(PlatformRainierClient __instance, ref NetworkStatus status)
        {
            if (PlatformRainierClient_DisableMultipleReconnections.AlreadyConnected && status == NetworkStatus.Reconnecting)
            {
                __instance.Disconnect();
                status = NetworkStatus.ReconnectFailed;
                return true;
            }
            if (status == NetworkStatus.Connected)
            {
                PlatformRainierClient_DisableMultipleReconnections.AlreadyConnected = true;
            }
            Plugin.SharedLogger.LogMessage(status);
            return true;
        }

        // Token: 0x0400001E RID: 30
        public static bool AlreadyConnected;
    }

    [HarmonyPatch(typeof(GameManager), "RestartProject")]
    internal static class GameManager_ResetNetworkConnectStatus
    {
        // Token: 0x0600003F RID: 63 RVA: 0x00002D6C File Offset: 0x00000F6C
        private static void Prefix()
        {
            PlatformRainierClient_DisableMultipleReconnections.AlreadyConnected = false;
        }
    }
}
