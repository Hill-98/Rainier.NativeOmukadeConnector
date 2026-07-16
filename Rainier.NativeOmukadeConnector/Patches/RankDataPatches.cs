using HarmonyLib;
using Omukade.Cheyenne.CustomMessages;
using RainierClientSDK.source.SeasonRank;
using SharedLogicUtils.source.Services.Query.Responses;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Rainier.NativeOmukadeConnector.Patches
{
    [HarmonyPatch]
    static class RankDataPatches
    {
        [HarmonyPatch(typeof(HUBLeagueTowerController), nameof(HUBLeagueTowerController.FetchSeasonDataFromConfigService))]
        [HarmonyPostfix]
        static void HUBLeagueTowerController_FetchSeasonDataFromConfigService(ref Task<bool> __result)
        {
            __result = __result.ContinueWith((task) =>
            {
                if (task.IsFaulted)
                {
                    throw task.Exception?.InnerException ?? task.Exception;
                }
                if (HUBLeagueTowerController.seasonConfigContent != null)
                {
                    HUBLeagueTowerController.seasonConfigContent.endDate = "2030-01-01T00:00:00Z";
                    HUBLeagueTowerController.seasonConfigContent.seasonTitleDate = "2026-01-01T00:00:00Z";
                    var list = HUBLeagueTowerController.seasonConfigContent.leagueConfigContent.leagueData;
                    foreach (var item in list)
                    {
                        foreach (var rank in item.rankData)
                        {
                            rank.expLimit = 10000;
                        }
                    }
                }
                return task.Result;
            });
        }

        [HarmonyPatch(typeof(SeasonRankQuery), nameof(SeasonRankQuery.UpdateInfoCache))]
        [HarmonyPrefix]
        static void SeasonRankQuery_UpdateInfoCache(SeasonRankQueryResponse response)
        {
            RankDataResponse data = GetRankExpFromOmukade();
            response.exp = data.exp;
            response.highestExp = data.highestExp;
        }

        internal static RankDataResponse GetRankExpFromOmukade()
        {
            RankDataResponse result = new RankDataResponse()
            {
                exp = 0,
                highestExp = 0,
            };
            if (!Plugin.Settings.GetRankData)
            {
                return result;
            }
            using ManualResetEvent m = new ManualResetEvent(initialState: false);
            Action<RankDataResponse> action = (RankDataResponse response) =>
            {
                result = response;
                m.Set();
            };

            ClientPatches.ReceivedRankExpResponse += action;

            try
            {
                WswCommon.InjectUpsockMessage(WswCommon.wswInstance, new GetRankData());
            }
            catch (Exception e)
            {
                BetterExceptionLogger.LogException(e);
            }

            m.WaitOne(new TimeSpan(hours: 0, minutes: 0, seconds: 10));

            ClientPatches.ReceivedRankExpResponse -= action;

            return result;
        }
    }
}