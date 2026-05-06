using UnityEngine;
using TMPro;
using HarmonyLib;

using LethalQuesting.Core;

namespace LethalQuesting.UI
{
    public class QuestUI
    {
        // function that creates the text
        public static void CreateTextOnHUD()
        {
            if (Plugin.myCustomText != null) return;

            GameObject hudCanvas = GameObject.Find("Systems/UI/Canvas");
            if (hudCanvas == null) hudCanvas = Object.FindObjectOfType<Canvas>()?.gameObject;
            if (hudCanvas == null) return;

            // 1. The main textbox container
            GameObject questContainer = new GameObject("QuestLogContainer");
            questContainer.transform.SetParent(hudCanvas.transform, false);
            questContainer.layer = 5;

            // 2. Background for the text box
            GameObject bgObj = new GameObject("QuestDebugBG");
            bgObj.transform.SetParent(questContainer.transform, false);
            UnityEngine.UI.Image debugImage = bgObj.AddComponent<UnityEngine.UI.Image>();
            debugImage.color = new Color(1f, 0f, 0f, 0.3f);
            bgObj.SetActive(Plugin.ConfigDebugFunctionality.Value); // only if debug enabled

            // 3. And this ones the text itself
            GameObject textObj = new GameObject("QuestText");
            textObj.transform.SetParent(questContainer.transform, false);
            Plugin.myCustomText = textObj.AddComponent<TextMeshProUGUI>();
            if (HUDManager.Instance != null && HUDManager.Instance.controlTipLines.Length > 0)
            {
                Plugin.myCustomText.font = HUDManager.Instance.controlTipLines[0].font;
                Plugin.myCustomText.fontSharedMaterial = HUDManager.Instance.controlTipLines[0].fontSharedMaterial;
            }
            
            string[] shipTexts = Plugin.ConfigShipText.Value.Split('$');
            string initialText = shipTexts.Length > 0 ? shipTexts[0] : " ";
            float initialAlpha = Plugin.ConfigUIAlphaShipText.Value;

            Plugin.myCustomText.text = initialText;
            Plugin.myCustomText.fontSize = Plugin.ConfigUIFontSize.Value;
            Plugin.myCustomText.color = new Color(0.8f, 0.8f, 0.8f, initialAlpha);
            Plugin.myCustomText.alignment = TextAlignmentOptions.TopRight;

            // 4. positioning and shaping the textbox
            RectTransform rect = questContainer.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 1);
            // offsets and boundaries are set by config values
            rect.anchoredPosition = new Vector2(-Plugin.ConfigUIOffsetX.Value, -Plugin.ConfigUIOffsetY.Value);
            rect.sizeDelta = new Vector2(Plugin.ConfigUIWidth.Value, Plugin.ConfigUIHeight.Value);
            
            FillParent(bgObj.GetComponent<RectTransform>());
            FillParent(textObj.GetComponent<RectTransform>());
        }
        
        // refreshes UI to be responsive to any changes made in config
        public static void RefreshUILayout()
        {
            if (Plugin.myCustomText == null || Plugin.myCustomText.transform.parent == null) return;

            GameObject container = Plugin.myCustomText.transform.parent.gameObject;
            RectTransform rect = container.GetComponent<RectTransform>();
            Plugin.myCustomText.fontSize = Plugin.ConfigUIFontSize.Value;

            if (rect != null)
            {
                rect.anchoredPosition = new Vector2(-Plugin.ConfigUIOffsetX.Value, -Plugin.ConfigUIOffsetY.Value);
                rect.sizeDelta = new Vector2(Plugin.ConfigUIWidth.Value, Plugin.ConfigUIHeight.Value);
            }
            
            Transform bg = container.transform.Find("QuestDebugBG");
            if (bg != null)
            {
                bg.gameObject.SetActive(Plugin.IsUIVisible && Plugin.ConfigDebugFunctionality.Value);
            }
        }
        
        // helper
        private static void FillParent(RectTransform r) {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.sizeDelta = Vector2.zero;
            r.anchoredPosition = Vector2.zero;
        }
        
        // force-toggles the ui off when player is in esc menu
        [HarmonyPatch(typeof(QuickMenuManager))]
        public class MenuTogglePatch
        {
            [HarmonyPatch("OpenQuickMenu")]
            [HarmonyPostfix]
            static void OnMenuOpen()
            {
                Plugin.IsEscMenuOpen = true;
                if (Plugin.myCustomText != null && Plugin.myCustomText.transform.parent != null)
                {
                    Plugin.myCustomText.transform.parent.gameObject.SetActive(false);
                }
            }
            
            [HarmonyPatch("CloseQuickMenu")]
            [HarmonyPostfix]
            static void OnMenuClose()
            {
                RefreshUILayout();
                Plugin.IsEscMenuOpen = false;
                if (Plugin.myCustomText != null && Plugin.myCustomText.transform.parent != null)
                {
                    Plugin.myCustomText.transform.parent.gameObject.SetActive(Plugin.IsUIVisible);
                }
            }
        }

        // Updates the text, re-generates if it was deleted as well
        public static void UpdateQuestLog(string Quests)
        {
            if (Plugin.myCustomText == null) CreateTextOnHUD();
            float alpha = GetTargetAlpha(Quests);
            Color cur = Plugin.myCustomText.color;
            Plugin.myCustomText.color = new Color(cur.r, cur.g, cur.b, alpha);
            Plugin.myCustomText.gameObject.SetActive(Plugin.IsUIVisible);
            Plugin.myCustomText.transform.SetAsLastSibling();
            Plugin.myCustomText.text = $"{Quests}";
            if(Plugin.ConfigOutputDebugLogs.Value) Plugin.mls.LogInfo($"UI updated!");
        }
        
        // Sets the text to be transparent if it matches one of the idle texts
        private static float GetTargetAlpha(string text)
        {
            string[] shipTexts = Plugin.ConfigShipText.Value.Split('$');
            foreach (string s in shipTexts)
            {
                if (!string.IsNullOrEmpty(s) && text.Contains(s))
                {
                    return Plugin.ConfigUIAlphaShipText.Value;
                }
            }
            return 1f;
        }
    }
}