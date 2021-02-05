using Kingmaker.Controllers;
using Kingmaker.PubSubSystem;
using ModMaker;
using ModMaker.Utility;
using System;
using System.Collections.Generic;
using System.Reflection;
using TurnBasedUpdated.Controllers;
using static TurnBasedUpdated.Main;
using static TurnBasedUpdated.Utility.SettingsWrapper;

namespace TurnBasedUpdated
{
    public class Core :
        IModEventHandler,
        ISceneHandler
    {
        internal Dictionary<AbilityExecutionProcess, TimeSpan> LastTickTimeOfAbilityExecutionProcess = new Dictionary<AbilityExecutionProcess, TimeSpan>();

        public BlueprintController Blueprints { get; internal set; }

        public ModCombatController Combat { get; internal set; }

        public HotkeyController Hotkeys { get; internal set; }

        public int Priority => 200;

        public UIController UI { get; internal set; }

        public bool Enabled {
            get => Mod.Settings.toggleTurnBasedUpdatedMode;
            set {
                if (Mod.Settings.toggleTurnBasedUpdatedMode != value)
                {
                    Mod.Debug(MethodBase.GetCurrentMethod(), value);

                    Mod.Settings.toggleTurnBasedUpdatedMode = value;
                    Blueprints.Update(true);
                    Combat.Reset(value);
                    EventBus.RaiseEvent<IWarningNotificationUIHandler>
                        (h => h.HandleWarning(value ? Local["UI_Txt_TurnBasedUpdatedMode"] : Local["UI_Txt_RealTimeMode"], false));
                }
            }
        }

        public static void FailedToPatch(MethodBase patch)
        {
            Type type = patch.DeclaringType;
            Mod.Warning($"Failed to patch '{type.DeclaringType?.Name}.{type.Name}.{patch.Name}'");
        }

        public void ResetSettings()
        {
            Mod.Debug(MethodBase.GetCurrentMethod());

            Mod.ResetSettings();
            Mod.Settings.lastModVersion = Mod.Version.ToString();
            LocalizationFileName = Local.FileName;
            Hotkeys?.Update(true, true);
            Blueprints?.Update(true);
        }

        private void HandleToggleTurnBasedUpdatedMode()
        {
            Enabled = !Enabled;
        }

        public void HandleModEnable()
        {
            Mod.Debug(MethodBase.GetCurrentMethod());

            if (!string.IsNullOrEmpty(LocalizationFileName))
            {
                Local.Import(LocalizationFileName, e => Mod.Error(e));
                LocalizationFileName = Local.FileName;
            }

            if (!Version.TryParse(Mod.Settings.lastModVersion, out Version version) || version < new Version(1, 0, 0))
                ResetSettings();
            else
                Mod.Settings.lastModVersion = Mod.Version.ToString();

            HotkeyHelper.Bind(HOTKEY_FOR_TOGGLE_MODE, HandleToggleTurnBasedUpdatedMode);
            EventBus.Subscribe(this);
        }

        public void HandleModDisable()
        {
            Mod.Debug(MethodBase.GetCurrentMethod());
            
            EventBus.Unsubscribe(this);
            HotkeyHelper.Unbind(HOTKEY_FOR_TOGGLE_MODE, HandleToggleTurnBasedUpdatedMode);
        }

        public void OnAreaBeginUnloading() { }

        public void OnAreaDidLoad()
        {
            Mod.Debug(MethodBase.GetCurrentMethod());

            LastTickTimeOfAbilityExecutionProcess.Clear();

            HotkeyHelper.Bind(HOTKEY_FOR_TOGGLE_MODE, HandleToggleTurnBasedUpdatedMode);
        }
    }
}