﻿using ModMaker;
using ModMaker.Utility;
using UnityEngine;
using UnityModManagerNet;
using static TurnBased.Main;
using static TurnBased.Utility.SettingsWrapper;

namespace TurnBased.Menus
{
    public class InterfaceOptions : IMenuSelectablePage
    {
        GUIStyle _buttonStyle;

        public string Name => "Interface";

        public int Priority => 300;

        public void OnGUI(UnityModManager.ModEntry modEntry)
        {
            if (Mod == null || !Mod.Enabled)
                return;

            if (_buttonStyle == null)
            {
                _buttonStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft };
            }

            OnGUICamera();

            GUILayout.Space(10f);

            OnGUIHUD();
        }

        private void OnGUICamera()
        {
            HighlightCurrentUnit =
                GUIHelper.ToggleButton(HighlightCurrentUnit,
                "Highlight Current Unit", _buttonStyle, GUILayout.ExpandWidth(false));

            CameraScrollToCurrentUnit =
                GUIHelper.ToggleButton(CameraScrollToCurrentUnit,
                "Camera Scroll To Current Unit On Turn Start", _buttonStyle, GUILayout.ExpandWidth(false));

            CameraLockOnCurrentPlayerUnit =
                GUIHelper.ToggleButton(CameraLockOnCurrentPlayerUnit,
                "Camera Lock On Current Player Unit", _buttonStyle, GUILayout.ExpandWidth(false));

            CameraLockOnCurrentNonPlayerUnit =
                GUIHelper.ToggleButton(CameraLockOnCurrentNonPlayerUnit,
                "Camera Lock On Current Non-Player Unit", _buttonStyle, GUILayout.ExpandWidth(false));
        }

        private void OnGUIHUD()
        {
            ShowAttackIndicatorOfCurrentUnit =
               GUIHelper.ToggleButton(ShowAttackIndicatorOfCurrentUnit,
               "Show Attack Indicator Of Current Unit", _buttonStyle, GUILayout.ExpandWidth(false));

            ShowAttackIndicatorOfPlayer =
                GUIHelper.ToggleButton(ShowAttackIndicatorOfPlayer,
                "Show Attack Indicator ... Of Player", _buttonStyle, GUILayout.ExpandWidth(false));

            ShowAttackIndicatorOfNonPlayer =
                GUIHelper.ToggleButton(ShowAttackIndicatorOfNonPlayer,
                "Show Attack Indicator ... Of Non-Player", _buttonStyle, GUILayout.ExpandWidth(false));

            ShowMovementIndicatorOfCurrentUnit =
                GUIHelper.ToggleButton(ShowMovementIndicatorOfCurrentUnit,
                "Show Movement Indicator Of Current Unit", _buttonStyle, GUILayout.ExpandWidth(false));

            ShowMovementIndicatorOfPlayer =
                GUIHelper.ToggleButton(ShowMovementIndicatorOfPlayer,
                "Show Movement Indicator ... Of Player", _buttonStyle, GUILayout.ExpandWidth(false));

            ShowMovementIndicatorOfNonPlayer =
                GUIHelper.ToggleButton(ShowMovementIndicatorOfNonPlayer,
                "Show Movement Indicator ... Of Non-Player", _buttonStyle, GUILayout.ExpandWidth(false));

            GUILayout.Space(10f);

            ShowAttackIndicatorOnHoverUI =
                GUIHelper.ToggleButton(ShowAttackIndicatorOnHoverUI,
                "Show Attack Indicator When Mouse Hover The UI Element", _buttonStyle, GUILayout.ExpandWidth(false));

            ShowMovementIndicatorOnHoverUI =
                GUIHelper.ToggleButton(ShowMovementIndicatorOnHoverUI,
                "Show Movement Indicator When Mouse Hover The UI Element", _buttonStyle, GUILayout.ExpandWidth(false));

            ShowIsFlatFootedIconOnHoverUI =
                GUIHelper.ToggleButton(ShowIsFlatFootedIconOnHoverUI,
                "Show An Icon To Indicate If The Unit Lost Dexterity Bonus To AC To Current Unit When Mouse Hover The UI Element", _buttonStyle, GUILayout.ExpandWidth(false));

            ShowIsFlatFootedIconOnUI =
                GUIHelper.ToggleButton(ShowIsFlatFootedIconOnUI,
                "Show An Icon To Indicate If The Unit Lost Dexterity Bonus To AC To Current Unit", _buttonStyle, GUILayout.ExpandWidth(false));

            GUILayout.Space(10f);

            SelectUnitOnClickUI =
                GUIHelper.ToggleButton(SelectUnitOnClickUI,
                "Select Unit When Click The UI Element", _buttonStyle, GUILayout.ExpandWidth(false));

            CameraScrollToUnitOnClickUI =
                GUIHelper.ToggleButton(CameraScrollToUnitOnClickUI,
                "Camera Scroll To Unit When Click The UI Element", _buttonStyle, GUILayout.ExpandWidth(false));

            ShowUnitDescriptionOnRightClickUI =
                GUIHelper.ToggleButton(ShowUnitDescriptionOnRightClickUI,
                "Show Unit Description When Right Click The UI Element", _buttonStyle, GUILayout.ExpandWidth(false));
            
            GUILayout.Space(10f);

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label($"HUD Width: {(int)HUDWidth:d3}", GUILayout.ExpandWidth(false));
                GUILayout.Space(5f);
                HUDWidth = 
                    GUIHelper.RoundedHorizontalSlider(HUDWidth, 0, 250f, 500f, GUILayout.Width(100f), GUILayout.ExpandWidth(false));
            }

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label($"HUD Max Units: {HUDMaxUnitsDisplayed:d2}", GUILayout.ExpandWidth(false));
                GUILayout.Space(5f);
                HUDMaxUnitsDisplayed = 
                    (int)GUIHelper.RoundedHorizontalSlider(HUDMaxUnitsDisplayed, 0, 5f, 25f, GUILayout.Width(100f), GUILayout.ExpandWidth(false));
            }
        }
    }
}