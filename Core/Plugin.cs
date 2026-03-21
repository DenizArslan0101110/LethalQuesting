using System.Runtime.Serialization;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using TMPro;
using BepInEx.Logging;
using CSync.Lib;
using CSync.Util;
using CSync.Extensions;
// In-Project Files 
using LethalQuesting.Utils;

namespace LethalQuesting.Core
{
    
    [BepInPlugin(GUID, Name, Version)]
    [BepInDependency("LethalNetworkAPI")]
    [BepInDependency("com.sigurd.csync")]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID = "com.fighter.lethalquesting";
        public const string Name = "Lethal Questing";
        public const string Version = "1.3.0";
        [DataContract]
        public class MyConfig : SyncedConfig2<MyConfig>
        {
            [DataMember] public SyncedEntry<float> ConfigPercentOfQuota { get; internal set; }
            [DataMember] public SyncedEntry<float> ConfigAverageQuestCount { get; internal set; }
            [DataMember] public SyncedEntry<bool> ConfigRandomNumberOfQuests { get; internal set; }

            public MyConfig(ConfigFile cfg) : base(Plugin.GUID)
            {
                ConfigPercentOfQuota = SyncedBindingExtensions.BindSyncedEntry(cfg, "Quests", "Rewards Percent of Quota", 0.1f, "Quest rewards use a percentage of your quota to calculate, there are other factors like scrap value and amount too. Default value is 10%.");
                ConfigAverageQuestCount = SyncedBindingExtensions.BindSyncedEntry(cfg, "Quests", "Number of Daily Quests", 1.5f, "Average amount of quests you get assigned per day, the actual amount will vary between 0 and double the value you put here. Values bigger than 2 may not be supported.");
                ConfigRandomNumberOfQuests = SyncedBindingExtensions.BindSyncedEntry(cfg, "Quests", "Randomize Number of Quests", true, "If enabled, it will use bell curve function and the [Number of Daily Quests] to assign a random amount of quests per day, if disabled it will round [Number of Daily Quests] to the closest integer and assign that many quests every day.");

                ConfigManager.Register(this);
            }
        }
        
        // config options
        public static MyConfig CustomConfig { get; set; } = null!;
        public static ConfigEntry<bool> ConfigDebugFunctionality { get; set; } = null!;
        public static ConfigEntry<bool> ConfigOutputDebugLogs { get; set; } = null!;
        public static ConfigEntry<int> ConfigUIWidth { get; set; } = null!;
        public static ConfigEntry<int> ConfigUIHeight { get; set; } = null!;
        public static ConfigEntry<int> ConfigUIOffsetX { get; set; } = null!;
        public static ConfigEntry<int> ConfigUIOffsetY { get; set; } = null!;
        public static ConfigEntry<int> ConfigUIFontSize { get; set; } = null!;
        
        public static bool IsUIVisible = true;
        public static bool IsEscMenuOpen = false;
        public static TextMeshProUGUI myCustomText;
        public static ManualLogSource mls;
        void Awake()
        {
            Harmony harmony = new Harmony("com.fighter.lethalquesting");
            NetworkHandler.Initialize();
            // config options
            CustomConfig = new MyConfig(Config);
            // debug
            ConfigOutputDebugLogs = Config.Bind<bool>("Debug", "Enable Debug Logs", false, "When enabled, it will print debug logs to terminal, any errors will still log regardless of this option.");
            ConfigDebugFunctionality = Config.Bind<bool>("Debug", "Color Textbox Background", false, "Enables debug features such as making the textbox object visible so that you can see in real-time if you configure offsets and boundaries. (toggle the text once you toggle this option so it updates)");
            // UI
            ConfigUIHeight = ConfigBindClamp("UI", "TextBox Height", 300, "Height of the textbox in pixels. (860 is whole screen, 0 is nonexistent)", 20, 860);
            ConfigUIWidth = ConfigBindClamp("UI", "TextBox Width", 280, "Width of the textbox in pixels. (520 is whole screen, 0 is nonexistent)", 20, 520);
            ConfigUIOffsetX = ConfigBindClamp("UI", "TextBox Horizontal Offset", 10, "Space between right edge of the screen and right edge of the textbox in pixels. (860 is whole screen, 0 is nonexistent)", 0, 860);
            ConfigUIOffsetY = ConfigBindClamp("UI", "TextBox Vertical Offset", 140, "Space between top of the screen and top of the textbox in pixels. (520 is whole screen, 0 is nonexistent)", 0, 520);
            ConfigUIFontSize = ConfigBindClamp("UI", "Text Font Size", 16, "Font size, if you make it too big make sure to scale the text box width and height too so it displays, it's 16 by default", 4, 120);
            
            mls = BepInEx.Logging.Logger.CreateLogSource("LQuesting");
            mls.LogInfo("Lethal Questing loaded!");
            harmony.PatchAll();
        }
        
        public ConfigEntry<int> ConfigBindClamp(string section, string key, int defaultValue, string description, int min, int max)
        {
            ConfigEntry<int> val = ((BaseUnityPlugin)this).Config.Bind<int>(section, key, defaultValue, description);
            val.Value = Mathf.Clamp(val.Value, min, max);
            return val;
        }
        
        public ConfigEntry<float> ConfigBindClamp(string section, string key, float defaultValue, string description, float min, float max)
        {
            ConfigEntry<float> val = ((BaseUnityPlugin)this).Config.Bind<float>(section, key, defaultValue, description);
            val.Value = Mathf.Clamp(val.Value, min, max);
            return val;
        }
        
        // keep around for now
        public static void LogLivingEnemies()
        {
            EnemyAI[] allEnemies = GameObject.FindObjectsOfType<EnemyAI>();

            if(ConfigOutputDebugLogs.Value) mls.LogInfo($"--- Current enemy number: {allEnemies.Length} ---");

            foreach (var enemy in allEnemies)
            {
                if (enemy == null || enemy.isEnemyDead) continue;
                
                string rawName = enemy.enemyType != null ? enemy.enemyType.enemyName : "Unknown";
                string enemyName = EnemyNameHelper.GetCleanName(rawName);
                float powerLevel = enemy.enemyType != null ? enemy.enemyType.PowerLevel : 0;
                int currentHP = enemy.enemyHP;

                if(ConfigOutputDebugLogs.Value) mls.LogInfo($"Enemy: {enemyName} | HP: {currentHP} | Power: {powerLevel}");
            }
        }
    }
}