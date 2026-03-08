using BepInEx;
using HarmonyLib;
using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using LethalNetworkAPI;
using Unity.Netcode;
using System.IO;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Image = UnityEngine.UI.Image;

namespace LethalQuesting
{
    public enum QuestType
    {
        None,
        Scrap,
        Kill,
        GroupSurvival,
        Betray,
        MovementProhibition
    }
    public struct QuestData : INetworkSerializable
    {
        public QuestType Type;
        public string Title;
        public string TargetName;
        public int TargetCount;
        public int CurrentCount;
        public int Reward;
        public bool IsRewardGiven;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Type);
            serializer.SerializeValue(ref Title);
            serializer.SerializeValue(ref TargetName);
            serializer.SerializeValue(ref TargetCount);
            serializer.SerializeValue(ref CurrentCount);
            serializer.SerializeValue(ref Reward);
            serializer.SerializeValue(ref IsRewardGiven);
        }
    }
    
    // data holding for scrap items
    public class ScrapData
    {
        public string Name;
        public int Value;
        public ulong NetworkId;
        public bool IsCollected;

        public ScrapData(GrabbableObject obj)
        {
            Name = obj.itemProperties.itemName;
            Value = obj.scrapValue;
            NetworkId = obj.NetworkObjectId;
            IsCollected = false;
        }
    }
    
    [HarmonyPatch(typeof(RoundManager))]
    public static class QuestManager
    {
        // decides which quest gets generated for the day
        [HarmonyPatch("SyncScrapValuesClientRpc")]
        [HarmonyPostfix]
        public static void GenerateDailyQuest(Unity.Netcode.NetworkObjectReference[] spawnedScrap, int[] allScrapValue)
        {
            if (!NetworkManager.Singleton.IsServer) return;
            
            //QuestType selectedType = (UnityEngine.Random.value > 0.5f) ? QuestType.Scrap : QuestType.Kill;
            QuestType selectedType = QuestType.Scrap; 

            if (selectedType == QuestType.Scrap)
            {
                ScrapManager.ScanMapForScraps(); 
                GenerateScrapQuest();
            }
            else if (selectedType == QuestType.Kill)
            {
                // MonsterManager.ScanMapForMonsters(); 
                // GenerateKillQuest();
                LethalQuesting.mls.LogInfo("Kill Quest!");
            }
        }

        // Checks scraps and fills in the information needed for a scrap quest
        private static void GenerateScrapQuest()
        {
            var todaysScraps = ScrapManager.TodaysScraps;
        
            if (todaysScraps == null || todaysScraps.Count == 0) return;
            
            int mode = UnityEngine.Random.Range(0, 3); 
            string[] parts = ScrapManager.ChooseScrapForQuest(todaysScraps, mode).Split('#');

            if (parts[0] == "None") return;

            QuestData newQuest = new QuestData
            {
                Type = QuestType.Scrap,
                TargetName = parts[0],
                TargetCount = int.Parse(parts[1]),
                CurrentCount = 0,
                Reward = CalculateReward(int.Parse(parts[1]), int.Parse(parts[2])),
                IsRewardGiven = false
            };
            
            LethalQuesting.ScrapQuestData.Value = newQuest;
            UpdateQuests.UpdateTheQuests(LethalQuesting.ScrapQuestData.Value);
            LethalQuesting.mls.LogInfo($"New Scrap Quest Generated: {newQuest.TargetName}");
        }

        // Calculation of quest reward
        private static int CalculateReward(int count, int totalValue)
        {
            if (count > 11)
            {   // shit happens sometimes, what if players decide to land on dine
                return (int)((5 + totalValue * 0.15));
            }
            else
            {
                return (int)((5 + totalValue * 0.25) * (2.1 + count / 10.0));
            }
        }
    }

    // handles calculations regarding Collect X scrap quests
    public static class ScrapManager
    {
        public static List<ScrapData> TodaysScraps = new List<ScrapData>();
        
        static void Postfix()
        {
            
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
                LethalQuesting.mls.LogInfo("Added " + obj.itemProperties.itemName);
            }

            LethalQuesting.mls.LogInfo($"Map scanned, {TodaysScraps.Count} scraps found.");
        }

        // finds the most common scrap and returns as string "ScrapName#Count#Reward"
        public static string ChooseScrapForQuest(List<ScrapData> TodaysScraps, int mode)
        {
            if (TodaysScraps == null || TodaysScraps.Count == 0)
                return "None#0";
            
            string finalName = "";
            int finalCount = 0;
            int finalTotalValue = 0;

            // mode 0 selects most numerous scrap
            if (mode == 0)
            {
                var chosen = TodaysScraps
                    // .Where(s => !s.IsCollected)
                    .GroupBy(s => s.Name)
                    .OrderByDescending(g => g.Count())
                    .Select(g => new { Name = g.Key, Count = g.Count() })
                    .FirstOrDefault();

                if (chosen != null)
                {
                    finalName = chosen.Name;
                    finalCount = chosen.Count;
                }
            }
            // mode 1 selects most valuable scrap type
            else if (mode == 1)
            {
                var highestValueItem = TodaysScraps
                    .OrderByDescending(s => s.Value)
                    .FirstOrDefault();

                if (highestValueItem != null)
                {
                    finalName = highestValueItem.Name;
                    finalCount = TodaysScraps.Count(s => s.Name == finalName);
                }
            }
            // else selects a random scrap type
            else
            {
                var randomGroup = TodaysScraps
                    // .Where(s => !s.IsCollected)
                    .GroupBy(s => s.Name)
                    .OrderBy(g => UnityEngine.Random.value)
                    .Select(g => new { Name = g.Key, Count = g.Count() })
                    .FirstOrDefault();

                if (randomGroup != null)
                {
                    finalName = randomGroup.Name;
                    finalCount = randomGroup.Count;
                }
            }
            
            if (!string.IsNullOrEmpty(finalName))
            {
                finalTotalValue = TodaysScraps
                    .Where(s => s.Name == finalName)
                    .Sum(s => s.Value);
            }
            
            return !string.IsNullOrEmpty(finalName) ? $"{finalName}#{finalCount}#{finalTotalValue}" : "None#0#0";
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
                    LethalQuesting.ItemDroppedMessage.SendServer(__instance.NetworkObjectId);
                }
            }
        }

        // This method gets triggered by everyone when someone drops an item but only host can continue past first line
        public static void CheckScrapCollection(ulong netId)
        {
            if (!RoundManager.Instance.IsServer) return;

            var matchingScrap = TodaysScraps.FirstOrDefault(s => s.NetworkId == netId);
            if (matchingScrap != null && !matchingScrap.IsCollected)
            {
                if (matchingScrap.Name == LethalQuesting.ScrapQuestData.Value.TargetName)
                {
                    matchingScrap.IsCollected = true;
                    
                    var updatedQuest = LethalQuesting.ScrapQuestData.Value;
                    updatedQuest.CurrentCount++;
                    LethalQuesting.ScrapQuestData.Value = updatedQuest;
                }
            }
        }
    }

    [BepInPlugin("com.fighter.lethalquesting", "Lethal Questing", "1.0.0")]
    [BepInDependency("LethalNetworkAPI")]
    public class LethalQuesting : BaseUnityPlugin
    {
        // Host knows and updates these, clients receive it to know quest progression
        public static LNetworkVariable<QuestData> ScrapQuestData = LNetworkVariable<QuestData>.Create("ScrapQuestData");

        // Client sends when they drop an item, host uses information to update quests
        public static LNetworkMessage<ulong> ItemDroppedMessage = LNetworkMessage<ulong>.Create("ItemDropped");

        public static TextMeshProUGUI myCustomText;
        public static ManualLogSource mls;

        // you know what this one does
        void Awake()
        {
            mls = BepInEx.Logging.Logger.CreateLogSource("LQuesting");
            
            ScrapQuestData.OnValueChanged += (oldVal, newVal) => 
            {
                UpdateQuests.UpdateTheQuests(newVal);
            };
            
            ItemDroppedMessage.OnServerReceived += (objId, senderId) =>
            {
                if (NetworkManager.Singleton.IsServer)
                {
                    ScrapManager.CheckScrapCollection(objId);
                }
            };

            Harmony harmony = new Harmony("com.fighter.lethalquesting");
            harmony.PatchAll();
            mls.LogInfo("Lethal Questing loaded!");
        }

        // function that creates the text
        public static void CreateTextOnHUD()
        {
            if (myCustomText != null) return;

            GameObject hudCanvas = GameObject.Find("Systems/UI/Canvas");
            if (hudCanvas == null)
            {
                Canvas mainCanvas = GameObject.FindObjectOfType<Canvas>();
                if (mainCanvas != null) hudCanvas = mainCanvas.gameObject;
            }

            if (hudCanvas == null) return;

            GameObject textObj = new GameObject("quest_text");
            textObj.transform.SetParent(hudCanvas.transform, false);
            textObj.layer = 5;

            myCustomText = textObj.AddComponent<TextMeshProUGUI>();

            if (HUDManager.Instance != null && HUDManager.Instance.controlTipLines.Length > 0)
            {
                myCustomText.font = HUDManager.Instance.controlTipLines[0].font;
                myCustomText.fontSharedMaterial = HUDManager.Instance.controlTipLines[0].fontSharedMaterial;
            }

            myCustomText.text = $"Welcome, land on a moon to see the quests, enjoy the ride!";
            myCustomText.fontSize = 16;
            myCustomText.color = new Color(0.8f, 0.8f, 0.8f);
            myCustomText.alignment = TextAlignmentOptions.Right;

            RectTransform rect = textObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
            rect.anchoredPosition = new Vector2(-10, -60);
            rect.sizeDelta = new Vector2(260, 360);

            textObj.transform.SetAsLastSibling();
        }

        // Updates the text, re-generates if it was deleted as well
        public static void UpdateQuestLog(string Quests)
        {
            if (myCustomText == null) CreateTextOnHUD();

            myCustomText.gameObject.SetActive(true);
            myCustomText.transform.SetAsLastSibling();

            myCustomText.text = $"{Quests}";
            mls.LogInfo($"Quest log updated!");
        }

        // a number string returns as one number above
        public static string IncrementStringNumber(string input)
        {
            if (int.TryParse(input, out int number))
            {
                return (number + 1).ToString();
            }
            return input;
        }
    }

    // makes text appear as soon as hud appears
    [HarmonyPatch(typeof(HUDManager), "Start")]
    public class HUDManagerPatch
    {
        [HarmonyPostfix]
        static void ShowHUD()
        {
            LethalQuesting.CreateTextOnHUD();
        }
    }
    

    // Kinda like main function, it is planned to finalize all quests here after their calcs are done
    public class UpdateQuests
    {
        public static void UpdateTheQuests(QuestData data)
        {
            if (data.Type == QuestType.None) return;

            string statusColor = data.IsRewardGiven ? "green" : "orange";
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

            string finalUI = $"<color={statusColor}>{taskText}</color>\n" +
                             $"<color={statusColor}>Reward: ■{data.Reward} {(data.IsRewardGiven ? "(Paid)" : "")}";

            LethalQuesting.UpdateQuestLog(finalUI);
            
            if (NetworkManager.Singleton.IsServer && data.CurrentCount >= data.TargetCount && !data.IsRewardGiven)
            {
                GiveReward(data);
            }
        }
        
        private static void GiveReward(QuestData data)
        {
            Terminal terminal = Object.FindObjectOfType<Terminal>();
            if (terminal != null)
            {
                terminal.groupCredits += data.Reward; 
                terminal.SyncGroupCreditsClientRpc(terminal.groupCredits, terminal.numberOfItemsInDropship);
                data.IsRewardGiven = true;
                
                LethalQuesting.ScrapQuestData.Value = data;

                LethalQuesting.mls.LogInfo($"[REWARD] {data.Reward} credits added to terminal.");
            }
        }
    }
}