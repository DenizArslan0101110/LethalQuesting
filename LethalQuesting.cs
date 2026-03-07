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
    public struct QuestData : INetworkSerializable
    {
        public string Name;
        public int Target;
        public int Collected;
        public int Reward;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Name);
            serializer.SerializeValue(ref Target);
            serializer.SerializeValue(ref Collected);
            serializer.SerializeValue(ref Reward);
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

    // handles calculations regarding Collect X scrap quests
    [HarmonyPatch(typeof(RoundManager))]
    public static class ScrapManager
    {
        // global list of scrap today
        public static List<ScrapData> TodaysScraps = new List<ScrapData>();

        [HarmonyPatch("SyncScrapValuesClientRpc")]
        [HarmonyPostfix]
        static void Postfix(Unity.Netcode.NetworkObjectReference[] spawnedScrap, int[] allScrapValue)
        {
            LethalQuesting.mls.LogInfo($"Level Loaded");
            // only host does this
            if (RoundManager.Instance.IsServer)
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
                // 25% chance most valuable scrap, 25% chance most numerous, 50% chance random
                int ScrapRandom = UnityEngine.Random.Range(1, 5);
                if (ScrapRandom == 1) ScrapRandom = 0;
                else if (ScrapRandom == 2) ScrapRandom = 1;
                else ScrapRandom = 3;

                string[] parts = ChooseScrapForQuest(TodaysScraps, ScrapRandom).Split('#');

                // Postfix içinde (Host kısmı)
                var newData = new QuestData {
                    Name = parts[0],
                    Target = int.Parse(parts[1]),
                    Collected = 0,
                    Reward = (int)((10 + int.Parse(parts[2]) / 2.0) * (0.80 * int.Parse(parts[1])/5))
                };
                LethalQuesting.ScrapQuestData.Value = newData;
            }

            LethalQuesting.mls.LogInfo("Going to update quests now...");
            UpdateQuests.UpdateTheQuests(LethalQuesting.ScrapQuestData.Value);
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
                if (matchingScrap.Name == LethalQuesting.ScrapQuestData.Value.Name)
                {
                    matchingScrap.IsCollected = true;
                    
                    var updatedQuest = LethalQuesting.ScrapQuestData.Value;
                    updatedQuest.Collected++;
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
            myCustomText.color = new Color(0.33f, 0.764f, 0.22f);
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

    // makes text appear as soon as hud appears (likely NOT deprecated)
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
            
            if (string.IsNullOrEmpty(data.Name) || data.Name == "None" || data.Target <= 0) 
            {
                LethalQuesting.mls.LogInfo($"UI update failure!!!");
                return; 
            }

            string ScrapQuestLine = $"Collect {data.Name} ({data.Collected}/{data.Target})\nReward: ■{data.Reward}";
            // this is not good, we give the reward every time the quest is completed when ui updates
            // since we only have one quest for the time being this wont cause problem right now but it is inevitably a problem
            if (data.Collected == data.Target)
            {
                Terminal terminal = Object.FindObjectOfType<Terminal>();
                terminal.groupCredits += data.Reward; 
                terminal.SyncGroupCreditsClientRpc(terminal.groupCredits, terminal.numberOfItemsInDropship);
            }
            // UI update
            LethalQuesting.UpdateQuestLog(ScrapQuestLine);
        
            LethalQuesting.mls.LogInfo($"UI update successful");
        }
    }
}