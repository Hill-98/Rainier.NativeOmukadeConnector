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

// #define DEBUG_LOG_RX_MESSAGES
using HarmonyLib;
using Newtonsoft.Json;
using Omukade.Cheyenne.CustomMessages;
using ClientNetworking;
using ClientNetworking.Stomp;
using ClientNetworking.Util;
using Rainier.NativeOmukadeConnector.Messages;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

namespace Rainier.NativeOmukadeConnector.Patches
{
    [HarmonyPatch(typeof(ClientBuilder))]
    internal static class ClientBuilderPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(ClientBuilder.Build), new Type[] { typeof(string) })]
        static void Build(ClientBuilder __instance)
        {
            __instance.SetWebsocketReceiptsEnabled(false);
#warning TODO: Implement receipts in Omukade
            Plugin.SharedLogger.LogInfo(nameof(ClientBuilderPatches) + ": Enable Message Receipts patched to: false");
        }
    }

    [HarmonyPatch(typeof(Client))]
    internal static class ClientPatches
    {
        internal static event Action<OnlineFriendsResponse>? ReceivedOnlineFriendsResponse;
        internal static MethodInfo forwardMessageForOFR = typeof(Client)
            .GetMethod("ForwardMessage", BindingFlags.Instance | BindingFlags.NonPublic)
            .MakeGenericMethod(typeof(OnlineFriendsResponse));

        internal static event Action<ImplementedExpandedCardsV1>? ReceivedImplementedExpandedCardsV1;
        internal static MethodInfo forwardMessageForExpandedCardsV1 = typeof(Client)
            .GetMethod("ForwardMessage", BindingFlags.Instance | BindingFlags.NonPublic)
            .MakeGenericMethod(typeof(ImplementedExpandedCardsV1));

        internal static HashSet<string>? ImplementedExpandedCardsFromServer = null;
        internal static string? ImplementedExpandedCardsFromServerChecksum = null;

        [HarmonyPostfix]
        [HarmonyPatch("CreateMessageHandlerMapping")]
        static void AddHanderForResponses(Client __instance, Dictionary<string, Action<string, ReusableBuffer>> __result)
        {
            //Plugin.SharedLogger.LogInfo($"Adding OFR frame to list of message handlers...");
            __result.Add(nameof(OnlineFriendsResponse), (contentType, buffer) =>
            {
                try
                {
                    forwardMessageForOFR.Invoke(__instance, new object[] { contentType, buffer, new MessageHandler<OnlineFriendsResponse>((c, e) => ReceivedOnlineFriendsResponse?.Invoke(e)) });
                }
                catch(Exception e)
                {
                    BetterExceptionLogger.LogException(e);
                    throw;
                }
            });

            __result.Add(nameof(ImplementedExpandedCardsV1), (contentType, buffer) =>
            {
                try
                {
                    forwardMessageForExpandedCardsV1.Invoke(__instance, new object[] { contentType, buffer, new MessageHandler<ImplementedExpandedCardsV1>((c, e) =>
                    {
                        ImplementedExpandedCardsFromServer = new HashSet<string>(e.ImplementedCardNames);
                        ImplementedExpandedCardsFromServerChecksum = e.Checksum;
                        ReceivedImplementedExpandedCardsV1?.Invoke(e);
                    }) });
                }
                catch (Exception e)
                {
                    BetterExceptionLogger.LogException(e);
                    throw;
                }
            });
        }

#if DEBUG_LOG_RX_MESSAGES
        [HarmonyPrefix]
        [HarmonyPatch("Dispatch")]
        static void LogRxMessage(StompFrame frame, ReusableBuffer buffer)
        {
#pragma warning disable Harmony003 // Harmony non-ref patch parameters modified
            string isJsonString = frame.ContentType == "application/json" ? "JSON" : "BINARY";
#pragma warning restore Harmony003 // Harmony non-ref patch parameters modified

            Plugin.SharedLogger.LogDebug($"RX {isJsonString} {frame.Payload} (len={buffer.Length}, cslen={buffer.ContentSegment.Count})");
        }
#endif
    }
}
