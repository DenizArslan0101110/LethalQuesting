using HarmonyLib;
using UnityEngine.InputSystem;
using UnityEngine;
using TMPro;
using GameNetcodeStuff;

using LethalQuesting.Core;
using LethalQuesting.Managers;
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

                if (Plugin.InputInstance.ToggleKey.WasPressedThisFrame())
                {
                    if (Plugin.myCustomText != null)
                    {
                        Plugin.IsUIVisible = !Plugin.IsUIVisible;
            
                        GameObject container = Plugin.myCustomText.transform.parent.gameObject;
                        container.SetActive(Plugin.IsUIVisible);

                        Transform bg = container.transform.Find("QuestDebugBG");
                        if (bg != null) bg.gameObject.SetActive(Plugin.ConfigDebugFunctionality.Value);
                    
                        if(Plugin.ConfigOutputDebugLogs.Value) 
                            Plugin.mls.LogInfo($"UI toggled to: {Plugin.IsUIVisible} via custom keybind");
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
            
            string[] shipTexts = Plugin.ConfigShipText.Value.Split('$');
            string chosenText = " ";
            if (shipTexts.Length > 0)
            {
                int randomIndex = UnityEngine.Random.Range(0, shipTexts.Length);
                chosenText = shipTexts[randomIndex];
            }
            
            string lobbyText = $"<color=#CCCCCC>{chosenText}</color>";
            QuestUI.UpdateQuestLog(lobbyText);
        }
    }
    
    [HarmonyPatch(typeof(VehicleController))]
    public class VehicleQuestPatch
    {
        // Flag so we dont spam the scan
        private static bool _hasScannedThisTrip = false;

        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        static void ScanItemsInVehicle(VehicleController __instance)
        {
            // Host only past this point
            if (!RoundManager.Instance.IsServer) return;
            
            // magneted for at least 1 seconds and we havent scanned already
            if (__instance.magnetedToShip && __instance.magnetTime >= 1f && !_hasScannedThisTrip)
            {
                _hasScannedThisTrip = true;
            
                // Find all items in car
                GrabbableObject[] itemsInVehicle = __instance.GetComponentsInChildren<GrabbableObject>();

                if (itemsInVehicle.Length > 0)
                {
                    if(Plugin.ConfigOutputDebugLogs.Value)
                        Plugin.mls.LogInfo($"[LethalQuesting] Cruiser on the magnet. {itemsInVehicle.Length} checking.");

                    foreach (var scrap in itemsInVehicle)
                    {
                        ScrapManager.CheckScrapCollection(scrap.NetworkObjectId);
                    }
                }
            }
        
            // Unmagneting the car should reset it back hopefully
            if (!__instance.magnetedToShip && _hasScannedThisTrip)
            {
                _hasScannedThisTrip = false;
            }
        }
    }
}