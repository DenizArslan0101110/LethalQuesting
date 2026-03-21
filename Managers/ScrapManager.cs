using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Netcode;
using HarmonyLib;
// In-Project Files 
using LethalQuesting.Models;
using LethalQuesting.Core;


namespace LethalQuesting.Managers
{
    // handles calculations regarding Collect X scrap quests
    public static class ScrapManager
    {
        public static List<ScrapData> TodaysScraps = new List<ScrapData>();
        public static List<ScrapPoolEntry> SelectedScrapPool = new List<ScrapPoolEntry>();

        public static void PrepareScrapPool()
        {
            SelectedScrapPool.Clear();

            if (TodaysScraps == null || TodaysScraps.Count == 0) return;
            
            var scrapGroups = TodaysScraps
                .GroupBy(s => s.Name)
                .Select(g => new ScrapPoolEntry { 
                    Name = g.Key, 
                    TotalCount = g.Count(), 
                    AverageValue = (int)g.Average(s => s.Value) 
                })
                .ToList();
            
            var shuffledGroups = scrapGroups.OrderBy(x => UnityEngine.Random.value).ToList();
            
            SelectedScrapPool = shuffledGroups.Take(5).ToList();
            
            for (int i = 0; i < SelectedScrapPool.Count; i++)
            {
                var entry = SelectedScrapPool[i];
                if(Plugin.ConfigOutputDebugLogs.Value) Plugin.mls.LogInfo($"[{i + 1}] {entry.Name} | Count: {entry.TotalCount} | Avg. Value: ■{entry.AverageValue}");
            }
        }

        // scans whole map (except the ship and player handhelds) for scrap items
        public static void ScanMapForScraps()
        {
            TodaysScraps.Clear();
            GrabbableObject[] allItems = GameObject.FindObjectsOfType<GrabbableObject>();
            foreach (var obj in allItems)
            {
                if (obj == null || !obj.itemProperties.isScrap) continue;

                var netObj = obj.GetComponent<NetworkObject>();
                if (netObj == null || !netObj.IsSpawned) continue;

                if (obj.isInShipRoom || obj.isHeld) continue;
                TodaysScraps.Add(new ScrapData(obj));
                //LethalQuesting.mls.LogInfo("Added " + obj.itemProperties.itemName);
            }

            if(Plugin.ConfigOutputDebugLogs.Value) Plugin.mls.LogInfo($"Map scanned, {TodaysScraps.Count} scraps found.");
        }

        // runs when anyone drops any item, checks if we collected the quests objective in the ship
        [HarmonyPatch(typeof(GrabbableObject), "DiscardItem")]
        public static class ScrapCollectPatch
        {
            [HarmonyPostfix]
            static void Postfix(GrabbableObject __instance)
            {
                if (__instance.itemProperties.isScrap && __instance.isInShipRoom)
                {
                    NetworkHandler.ItemDroppedMessage.SendServer(__instance.NetworkObjectId);
                }
            }
        }

        // This method gets triggered by everyone when someone drops an item but only host can continue past first line
        // it receives netId of dropped item and checks scrap quests to see if it is required by collection quests
        // insane amount of debug logging here
        public static void CheckScrapCollection(ulong netId)
        {
            if (!RoundManager.Instance.IsServer) return;
            
            //LethalQuesting.mls.LogInfo($"--- CheckScrapCollection Triggered! ID: {netId} ---");
            //LethalQuesting.mls.LogInfo($"TodaysScraps List has {TodaysScraps.Count} items.");
            
            var matchingScrap = TodaysScraps.FirstOrDefault(s => s.NetworkId == netId);
            
            if (matchingScrap == null)
            {
                //LethalQuesting.mls.LogInfo($"item with ID {netId} not found in TodaysScraps!");
                return;
            }
            
            if (matchingScrap.IsCollected)
            {
                //LethalQuesting.mls.LogInfo($"{matchingScrap.Name} item marked as already collected!");
                return;
            }
            
            //LethalQuesting.mls.LogInfo($"Found a match: {matchingScrap.Name}. Checking quests");
            
            var questList = NetworkHandler.TodaysQuestsData.Value;
            if (questList == null)
            {
                //LethalQuesting.mls.LogError("TodaysQuestsData.Value NULL!");
                return;
            }
            
            bool hasChanged = false;
            for (int i = 0; i < questList.Count; i++)
            {
                QuestData currentQuest = questList[i];
                
                //LethalQuesting.mls.LogInfo($"QuestTarget({currentQuest.TargetName}) vs ScrapName({matchingScrap.Name})");

                if (currentQuest.Type == QuestType.Scrap && currentQuest.TargetName == matchingScrap.Name)
                {
                    currentQuest.CurrentCount++;
                    questList[i] = currentQuest;
                    hasChanged = true;
                    //LethalQuesting.mls.LogInfo($"QUEST PROGRESS: {currentQuest.TargetName} count increased to -> {currentQuest.CurrentCount}");
                }
            }

            if (hasChanged)
            {
                matchingScrap.IsCollected = true;
                NetworkHandler.TodaysQuestsData.Value = new List<QuestData>(questList);
                if(Plugin.ConfigOutputDebugLogs.Value) Plugin.mls.LogInfo("Quest list updated on the network.");
            }
            /*else
            {
                LethalQuesting.mls.LogWarning($"Item found matching with a quest!({matchingScrap.Name}) but somehow no quests require the item?!");
            }*/
        }
    }
}