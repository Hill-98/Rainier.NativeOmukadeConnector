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

using _Rainier.Scripts.Managers.StartUp;
using HarmonyLib;
using Newtonsoft.Json;
using Omukade.Cheyenne.CustomMessages;
using ClientNetworking;
using ClientNetworking.Codecs;
using ClientNetworking.Models;
using ClientNetworking.Models.GameServer;
using ClientNetworking.Models.Matchmaking;
using ClientNetworking.Models.Query;
using ClientNetworking.Models.User;
using ClientNetworking.Models.WebSocket;
using Rainier.NativeOmukadeConnector.Messages;
using RainierClientSDK;
using RainierClientSDK.Inventory;
using SharedLogicUtils.DataTypes;
using SharedLogicUtils.source.Services.Query.Contexts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TPCI.Commands;
using TPCI.Localization;

namespace Rainier.NativeOmukadeConnector.Patches
{
    /// <summary>
    /// Holds common fields used for WebsocketWrapper patches, helpers for interacting with WSW objects, and reflection information for WSW fields/methods.
    /// </summary>
    internal static class WswCommon
    {
        internal static Assembly platformSdkAssembly = typeof(WebSocketSettings).Assembly;
        internal static Type wswType = platformSdkAssembly.GetType("ClientNetworking.WebsocketWrapper");

        internal static MethodInfo _SendCommandGeneric = wswType.GetMethod("SendCommand", BindingFlags.Instance | BindingFlags.Public);

        internal static ICodec JsonCodec = (ICodec) platformSdkAssembly.GetType("ClientNetworking.Codecs.CodecUtil").GetMethod("Codec", BindingFlags.Static | BindingFlags.Public).Invoke(null, new object[] { SerializationFormat.JSON });

        internal static object? wswInstance = null;

        internal static Client ResolveClient()
        {
            Delegate dispatcher = (Delegate)wswInstance.GetType().GetField("_dispatcher", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(wswInstance);
            Client clientTarget = (Client)dispatcher.Target;
            return clientTarget;
        }

        /// <summary>
        /// Injects a message into the current WebsocketWrapper. If hooking OnOpen, use <see cref="InjectUpsockMessageBypassingConnectionCheck"/> due to not-yet-set fields preventing transmission.
        /// </summary>
        internal static void InjectUpsockMessage<T>(object wswWrapper, T message) where T : class
        {
            Command<T> commandToSend = new Command<T>("/omukade10/sdm", reliable: false);
            WswCommon._SendCommandGeneric.MakeGenericMethod(typeof(T)).Invoke(wswWrapper, new object[] { commandToSend, message });
        }

        /// <summary>
        /// Injects a message into the current WebsocketWrapper. If hooking OnOpen, use <see cref="InjectUpsockMessageBypassingConnectionCheck"/> due to not-yet-set fields preventing transmission.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="client"></param>
        /// <param name="message"></param>
        internal static void InjectUpsockMessage<T>(IClient client, T message) where T : class
            => InjectUpsockMessage<T>(Traverse.Create(client).Field("_ws").GetValue(), message);

        /// <summary>
        /// Forces the WebsocketWrapper to think it's connected for purposes of WebsocketWrapper.SendCommand. If hooking WebsocketClient.OnOpen, this field is not yet set and messages will fail to send.
        /// </summary>
        /// <param name="client"></param>
        internal static void ForceIsConnectedOnWsw(IClient client)
            => Traverse.Create(client).Field("_ws").Field("_connected").SetValue(true);
    }

    /// <summary>
    /// Rewrites the websocket endpoint to point to the Omukade server.
    /// </summary>
    [HarmonyPatch]
    public static class HttpRouter_WebsocketEndpoint
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            return Enumerable.Repeat(WswCommon.platformSdkAssembly.GetType("ClientNetworking.HttpRouter").GetMethod("WebsocketEndpoint", BindingFlags.Instance | BindingFlags.Public), 1);
        }

        static bool Prefix(ClientNetworking.IdeRoute ____route, ref Uri __result)
        {
            string endpointToUse = Plugin.Settings.OmukadeEndpoint + "/websocket/v1/external/stomp";
            Plugin.SharedLogger.LogDebug($"Rewriting Websocket route from \"{____route.WebsocketUrl}\" to \"{endpointToUse}\"");
            __result = new Uri(endpointToUse);
            return false;
        }
    }

    /// <summary>
    /// Forces an SDM with deck information to be sent for all matchmaking-related messages, and swallows session messages that aren't used in Omukade.
    /// </summary>
    [HarmonyPatch]
    static class Wsw_SendCommand
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            Type[] allHandledCommands = new Type[] {
                typeof(BeginMatchmaking),
                typeof(ProposeDirectMatch),
                typeof(AcceptDirectMatch),

                typeof(ClientNetworking.Models.Account.SessionUpdatePayload),
                typeof(SessionStart),
            };

            return allHandledCommands.Select(typeUsed => WswCommon.wswType.GetMethod("SendCommand", BindingFlags.Instance | BindingFlags.Public).MakeGenericMethod(typeUsed));
        }

        [HarmonyPrefix]
        [HarmonyPatch]
        public static bool InjectSdmMessagesToMatchmakingMessages(object __instance, ref object command, ref object body)
        {
            if (body is ClientNetworking.Models.Account.SessionUpdatePayload || body is SessionStart)
            {
                Plugin.SharedLogger.LogDebug($"Intentionally swallowing sensitive message {body.GetType().Name} that shouldn't be sent to Omukade.");
                return false;
            }

            string deckId = null;

            // If it relates to starting a game, decode the deck ID
            if (body is BeginMatchmaking bm)
            {
                MatchmakingContext mc = JsonConvert.DeserializeObject<MatchmakingContext>(System.Text.Encoding.UTF8.GetString(bm.context));
                deckId = mc.deckID;
            }
            else if (body is ProposeDirectMatch pdm)
            {
                FriendDirectMatchContext fdmc = JsonConvert.DeserializeObject<FriendDirectMatchContext>(System.Text.Encoding.UTF8.GetString(pdm.context));
                deckId = fdmc.deckID;
            }
            else if (body is AcceptDirectMatch adm)
            {
                FriendDirectMatchContext fdmc = JsonConvert.DeserializeObject<FriendDirectMatchContext>(System.Text.Encoding.UTF8.GetString(adm.context));
                deckId = fdmc.deckID;
            }

            if (deckId != null && ReferenceGetters.collectionServiceReference != null)
            {
                Plugin.SharedLogger.LogDebug("Packet includes a Deck ID; fetching deck details to inject SDM...");
                CollectionData deckListCollection = ReferenceGetters.collectionServiceReference.GetCollectionAsync(service: new PlatformInventoryService(WswCommon.ResolveClient(), null), deckId).Result;

                SupplementalDataMessageV2 sdm = new SupplementalDataMessageV2 { DeckInformation = deckListCollection, OutfitInformation = InventoryService.currentOutfit, CurrentRegion = WswCommon.ResolveClient().CurrentRegion };
                WswCommon.InjectUpsockMessage(__instance, sdm);
            }

            return true;
        }
    }

    /// <summary>
    /// Forces an SDM to be sent immediately after opening a Websocket connection with the player's ID and display name
    /// </summary>
    [HarmonyPatch]
    static class Wsw_CreateWebsocket
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            return Enumerable.Repeat(WswCommon.wswType.GetMethod("CreateWebsocket", BindingFlags.Instance | BindingFlags.NonPublic), 1);
        }

        [HarmonyPatch]
        [HarmonyPostfix]
        static void Postfix(object __instance, object __result)
        {
            WswCommon.wswInstance = __instance;

            // result is WebsocketClient, a private type
            Type wsOpenEventHandlerType = WswCommon.platformSdkAssembly
            .GetType("ClientNetworking.WebsocketClient")
            .GetNestedType("WebSocketOpenEventHandler");

            EventInfo onOpenEvent = WswCommon.platformSdkAssembly
            .GetType("ClientNetworking.WebsocketClient")
            .GetEvent("OnOpen");

            Client parentClient = Traverse.Create(__instance).Field("_dispatcher").GetValue<Delegate>().Target as Client;

            Delegate onOpenHandler = Delegate.CreateDelegate(wsOpenEventHandlerType, new OnOpenEventHandlerDelegate { parentClient = parentClient }, nameof(OnOpenEventHandlerDelegate.FireEvent));
            onOpenEvent.AddEventHandler(__result, onOpenHandler);
        }

        private class OnOpenEventHandlerDelegate
        {
            internal Client parentClient;

            internal void FireEvent()
            {
                string screenName = ManagerSingleton<LoginManager>.instance.loginData.UserData.screen_name;

                System.Threading.Thread.Sleep(250 /*ms*/);
                Plugin.SharedLogger.LogDebug($"Sending SDM[screenname={screenName},playerid={parentClient.AccountId}]");
                try
                {
                    WswCommon.ForceIsConnectedOnWsw(parentClient);
                    WswCommon.InjectUpsockMessage(client: parentClient, new SupplementalDataMessageV2 { PlayerDisplayName = screenName, PlayerId = parentClient.AccountId });

                    if(Plugin.Settings.AskServerForImplementedCards)
                    {
                        WswCommon.InjectUpsockMessage(client: parentClient, new GetImplementedExpandedCardsV1());
                    }
                }
                catch(Exception e)
                {
                    BetterExceptionLogger.LogException(e);
                    throw;
                }
            }
        }
    }
}
