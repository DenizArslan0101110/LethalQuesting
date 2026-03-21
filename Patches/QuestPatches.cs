using HarmonyLib;
using UnityEngine.InputSystem;
using UnityEngine;
using TMPro;
using GameNetcodeStuff;

using LethalQuesting.Core;
using LethalQuesting.UI;

namespace LethalQuesting.Patches
{
    [HarmonyPatch(typeof(TimeOfDay), "Start")]
    public class RegisterHourPatch
    {
        static void Postfix(TimeOfDay __instance)
        {
            __instance.onHourChanged.AddListener(() => {
                Plugin.LogLivingEnemies();
            });
        }
    }
        
    [HarmonyPatch(typeof(HUDManager), "Update")]
    public static class KeyboardShortcutPatch
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            try
            {
                if (HUDManager.Instance == null || Plugin.IsEscMenuOpen) return;
                
                var localPlayer = GameNetworkManager.Instance.localPlayerController;
                if (localPlayer != null)
                {
                    // Terminal or chat should invalidate toggle key
                    if (localPlayer.isTypingChat || localPlayer.inTerminalMenu) return;
                }

                if (Keyboard.current.kKey.wasPressedThisFrame)
                {
                    if (Plugin.myCustomText != null)
                    {
                        Plugin.IsUIVisible = !Plugin.IsUIVisible;
                
                        // standard toggle business, removes ui if key pressed
                        GameObject container = Plugin.myCustomText.transform.parent.gameObject;
                        container.SetActive(Plugin.IsUIVisible);

                        // Sets background active if we have deebug enabled
                        Transform bg = container.transform.Find("QuestDebugBG");
                        if (bg != null) bg.gameObject.SetActive(Plugin.ConfigDebugFunctionality.Value);
                        
                        if(Plugin.ConfigOutputDebugLogs.Value) Plugin.mls.LogInfo($"UI toggled to: {Plugin.IsUIVisible}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Plugin.mls.LogError($"Update error: {ex}");
            }
        }
    }
    // makes text appear as soon as hud appears
    [HarmonyPatch(typeof(HUDManager), "Start")]
    public class HUDManagerPatch
    {
        [HarmonyPostfix]
        static void ShowHUD()
        {
            QuestUI.CreateTextOnHUD();
        }
    }
    // set the text to default and reset quest flags
    [HarmonyPatch(typeof(StartOfRound))]
    public static class ShipLeavePatch
    {
        [HarmonyPatch("EndOfGame")]
        [HarmonyPostfix]
        static void SetLobbyText(int bodiesInsured, int connectedPlayersOnServer, int scrapCollected)
        {
            Managers.QuestManager.QuestsGeneratedForThisRound = false;
            string lobbyText = "<color=#CCCCCC>Press K to toggle quest texts at anytime.</color>";
            QuestUI.UpdateQuestLog(lobbyText);
        }
    }
}