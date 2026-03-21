using HarmonyLib;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using System.Text;
// In-Project Files 
using LethalQuesting.Core;
using LethalQuesting.Utils;
using LethalQuesting.Models;
using LethalQuesting.UI;

namespace LethalQuesting.Managers
{
    [HarmonyPatch(typeof(RoundManager))]
    public static class QuestManager
    {
        public static bool QuestsGeneratedForThisRound = false;
        // decides which quest gets generated for the day
        [HarmonyPatch("SyncScrapValuesClientRpc")]
        [HarmonyPostfix]
        public static void GenerateDailyQuest(Unity.Netcode.NetworkObjectReference[] spawnedScrap, int[] allScrapValue)
        {
            if (!NetworkManager.Singleton.IsServer) return;
            if (QuestsGeneratedForThisRound) return;
            
            NetworkHandler.TodaysQuestsData.Value = new List<QuestData>();

            // determines how many quests we get today
            int amountOfQuestsToday = MathUtils.SimpleBellCurve(0.0f, Plugin.CustomConfig.ConfigAverageQuestCount.Value*2.0f);
            
            // prep for kill quest

            
            // prep for scrap quest
            Managers.ScrapManager.ScanMapForScraps();
            Managers.ScrapManager.PrepareScrapPool(); 
            if(Plugin.ConfigOutputDebugLogs.Value) Plugin.mls.LogInfo($"Crew rolled {amountOfQuestsToday} quests!");
            List<QuestData> finalQuests = new List<QuestData>();

            for (int i=0; i < amountOfQuestsToday; i++)
            {
                QuestData? q = GenerateScrapQuestFromPool();
                if(q.HasValue) finalQuests.Add(q.Value);
            }

            NetworkHandler.TodaysQuestsData.Value = finalQuests;
            QuestsGeneratedForThisRound = true;
        }

        // Checks scraps and fills in the information needed for a scrap quest
        private static QuestData? GenerateScrapQuestFromPool()
        {
            if (Managers.ScrapManager.SelectedScrapPool.Count == 0) return null;

            ScrapPoolEntry entry = Managers.ScrapManager.SelectedScrapPool[0];
            Managers.ScrapManager.SelectedScrapPool.RemoveAt(0);

            // TODO: make this like a bell curve or smth idk 50% and 90% unified random doesnt sit right with me
            int targetCount = Mathf.Max(1, Mathf.RoundToInt(entry.TotalCount * UnityEngine.Random.Range(0.3f, 0.8f)));
            
            return new QuestData
            {
                Type = QuestType.Scrap,
                Title = $"Priority Retrieval: {entry.Name}",
                TargetName = entry.Name,
                TargetCount = targetCount,
                CurrentCount = 0,
                Reward = CalculateReward(targetCount, entry.AverageValue),
                IsRewardGiven = false
            };
        }

        // Calculation of quest reward
        private static int CalculateReward(int count, int averageValue)
        {
            return (int)(TimeOfDay.Instance.profitQuota * Plugin.CustomConfig.ConfigPercentOfQuota.Value * (averageValue/100.0f + count * 0.4));
            // "There must absolutely be no formulas that work for 7-Dine"  - Zeekerss (during the development of v73 update)
        }
    }
    
    // Kinda like main function, it is planned to finalize all quests here after their calcs are done
    public class UpdateQuests
    {
        public static void UpdateTheQuests(List<QuestData> questList)
        {
            if (questList == null || questList.Count == 0) 
            {
                QuestUI.UpdateQuestLog("No missions today.");
                return;
            }

            StringBuilder fullLogBuilder = new StringBuilder();

            for (int i = 0; i < questList.Count; i++)
            {
                var data = questList[i];
                if (data.Type == QuestType.None) continue;
                
                string statusColor = (data.CurrentCount >= data.TargetCount) ? "green" : "orange";
                string taskText = "";
        
                switch (data.Type)
                {
                    case QuestType.Scrap:
                        taskText = $"Collect {data.TargetName}: {data.CurrentCount}/{data.TargetCount}";
                        break;
                    case QuestType.Kill:
                        taskText = $"Exterminate {data.TargetName}: {data.CurrentCount}/{data.TargetCount}";
                        break;
                    default:
                        taskText = $"{data.Title}: {data.CurrentCount}/{data.TargetCount}";
                        break;
                }
                
                fullLogBuilder.AppendLine($"<color={statusColor}>{taskText}</color>");
                fullLogBuilder.AppendLine($"<color={statusColor}>Reward: ■{data.Reward} {((data.CurrentCount >= data.TargetCount) ? "(Paid)" : "")}</color>");
                fullLogBuilder.AppendLine();
                
                if (NetworkManager.Singleton.IsServer && data.CurrentCount >= data.TargetCount && !data.IsRewardGiven)
                {
                    GiveReward(i); 
                }
            }
            QuestUI.UpdateQuestLog(fullLogBuilder.ToString());
        }
        
        // given the index of completed quest, it will reward the crew accordingly
        private static void GiveReward(int questIndex)
        {
            Terminal terminal = Object.FindObjectOfType<Terminal>();
            if (terminal == null) return;
            var currentList = NetworkHandler.TodaysQuestsData.Value;
            if (questIndex < 0 || questIndex >= currentList.Count) return;
            QuestData data = currentList[questIndex];
            if (data.IsRewardGiven) return;
            terminal.groupCredits += data.Reward; 
            terminal.SyncGroupCreditsClientRpc(terminal.groupCredits, terminal.numberOfItemsInDropship);
            data.IsRewardGiven = true;
            currentList[questIndex] = data;
            NetworkHandler.TodaysQuestsData.Value = currentList;
            UpdateTheQuests(currentList);
            if(Plugin.ConfigOutputDebugLogs.Value) Plugin.mls.LogInfo($"{data.Reward} credits added for the quest: {data.TargetName}");
        }
    }
}