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
using ClientNetworking;
using ClientNetworking.Codecs;
using ClientNetworking.Models.Matchmaking;
using ClientNetworking.Models.Routing;
using ClientNetworking.Models.WebSocket;
using ClientNetworking.Stomp;
using ClientNetworking.Util;
using HarmonyLib;
using Newtonsoft.Json;
using Omukade.Cheyenne.CustomMessages;
using Rainier.NativeOmukadeConnector.Messages;
using RainierClientSDK.Inventory;
using SharedLogicUtils.DataTypes;
using SharedLogicUtils.source.Services.Query.Contexts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

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

    [HarmonyPatch(typeof(HttpRouter), nameof(HttpRouter.UpdateRoute))]
    public static class HttpRouter_UpdateRoute
    {
        public static QueryRouteResponse Response = null;

        static void Prefix(HttpRouter __instance, QueryRouteResponse routeResponse)
        {
            if (__instance != WebsocketWrapper_OpenAsync.router)
            {
                Response = routeResponse;
            }
        }
    }

    /// <summary>
    /// Rewrites the websocket endpoint to point to the Omukade server.
    /// </summary>
    [HarmonyPatch]
    public static class HttpRouter_WebsocketEndpoint
    {
        public static Uri OrigWebsocketUrl = null;
        static IEnumerable<MethodBase> TargetMethods()
        {
            return Enumerable.Repeat(WswCommon.platformSdkAssembly.GetType("ClientNetworking.HttpRouter").GetMethod("WebsocketEndpoint", BindingFlags.Instance | BindingFlags.Public), 1);
        }

        static bool Prefix(HttpRouter __instance, ClientNetworking.IdeRoute ____route, ref Uri __result)
        {
            if (__instance == WebsocketWrapper_OpenAsync.router)
            {
                return true;
            }
            OrigWebsocketUrl = ____route.WebsocketUrl;
            string endpointToUse = Plugin.Settings.OmukadeEndpoint + "/websocket/v1/external/stomp";
            Plugin.SharedLogger.LogDebug($"Rewriting Websocket route from \"{____route.WebsocketUrl}\" to \"{endpointToUse}\"");
            __result = new Uri(endpointToUse);
            return false;
        }
    }

    [HarmonyPatch(typeof(WebsocketWrapper), nameof(WebsocketWrapper.MakeWebsocketEndpointHeaders))]
    public static class WebsocketWrapper_MakeWebsocketEndpointHeaders
    {
        static void Postfix(WebsocketWrapper __instance, ref Dictionary<string, string> __result)
        {
            if (__instance == WebsocketWrapper_OpenAsync.wrapper)
            {
                Plugin.SharedLogger.LogInfo("WebsocketWrapper_MakeWebsocketEndpointHeaders: break wrapper");
                return;
            }
            __result.Remove("Authorization");
            __result.Remove("x-user-flags");
        }
    }

    [HarmonyPatch(typeof(WebsocketWrapper), nameof(WebsocketWrapper.OpenAsync))]
    public static class WebsocketWrapper_OpenAsync
    {
        public static WebsocketWrapper wrapper = null;
        public static HttpRouter router = null;

        static void Postfix(WebsocketWrapper __instance)
        {
            if (wrapper == null)
            {
                try
                {
                    router = new HttpRouter(__instance._router._stage);
                    router.SetServiceGroup(__instance._router.ServiceGroup);
                    router.UpdateRoute(HttpRouter_UpdateRoute.Response);
                    WebsocketPersistent persistent = new WebsocketPersistent(true, true, true);
                    wrapper = new WebsocketWrapper(__instance._logger, router, __instance._token, new WebsocketWrapper.MessageDispatcher(Dispatch), __instance._serializer, __instance._settings, persistent, new NetworkStatusChangeHandler(ChangeNetworkStatus), new DisconnectRationaleHandler(ReportDisconnectRationale), new ServerTimeAvailable(() => { }), IncrementMetric, __instance._userAgentString);
                    Task task = wrapper.OpenAsync();
                    task.ContinueWith((t) =>
                    {
                        Plugin.SharedLogger.LogInfo("WebsocketWrapper_OpenAsync Postfix task done.");
                        Plugin.SharedLogger.LogError(t.Exception);
                    });
                } catch(Exception e) {
                    Plugin.SharedLogger.LogError(e);
                }
            }
        }

        public static void ChangeNetworkStatus(NetworkStatus newStatus)
        {
        }

        public static void Dispatch(ref StompFrame frame, ReusableBuffer buffer)
        {
        }

        public static void IncrementMetric(string name, string info)
        {
        }

        public static void ReportDisconnectRationale(DisconnectRationale rationale)
        {
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
            if (__instance == WebsocketWrapper_OpenAsync.wrapper)
            {
                Plugin.SharedLogger.LogInfo("Wsw_SendCommand: break wrapper");
                return true;
            }
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
            if (__instance == WebsocketWrapper_OpenAsync.wrapper)
            {
                Plugin.SharedLogger.LogInfo("Wsw_CreateWebsocket: break wrapper");
                return;
            }
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
