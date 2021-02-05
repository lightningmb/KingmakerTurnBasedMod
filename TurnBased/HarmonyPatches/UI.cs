﻿using Harmony12;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UI.Inspect;
using Kingmaker.UI.Selection;
using Kingmaker.UI.Tooltip;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.View;
using ModMaker.Utility;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TurnBasedUpdated.Utility;
using UnityEngine;
using static ModMaker.Utility.ReflectionCache;
using static TurnBasedUpdated.Main;
using static TurnBasedUpdated.Utility.StatusWrapper;

namespace TurnBasedUpdated.HarmonyPatches
{
    static class UI
    {
        // show a small circle under a unit if the unit is within attack range
        [HarmonyPatch(typeof(UIDecal), nameof(UIDecal.HandleAoEMove), typeof(Vector3), typeof(AbilityData))]
        static class UIDecal_HandleAoEMove_Patch
        {
            [HarmonyPrefix]
            static bool Prefix(UIDecal __instance, Vector3 pos, AbilityData abilityData)
            {
                if (IsInCombat() && abilityData == null)
                {
                    UnitEntityData unit = Mod.Core.UI.AttackIndicator.Unit;
                    __instance.SetHoverVisibility(unit.CanTarget(__instance.Unit, pos.x, pos.y != 0f, pos.z != 0f));

                    return false;
                }
                return true;
            }
        }

        // make Unit.Inspect() extension work
        [HarmonyPatch(typeof(InspectController), nameof(InspectController.HandleUnitRightClick), typeof(UnitEntityView))]
        static class InspectController_HandleUnitRightClick_Patch
        {
            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpiler(MethodBase original, IEnumerable<CodeInstruction> codes, ILGenerator il)
            {
                // ---------------- before ----------------
                // m_TooltipTrigger.SetObject(entityData);
                // ---------------- after  ----------------
                // m_TooltipTrigger.SetObject(entityData);
                // m_TooltipTrigger.ShowTooltipManual(true);
                CodeInstruction[] findingCodes = new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld,
                        GetFieldInfo<InspectController, TooltipTrigger>("m_TooltipTrigger")),
                    new CodeInstruction(OpCodes.Ldloc_0),
                    new CodeInstruction(OpCodes.Callvirt,
                        GetMethodInfo<TooltipTrigger, Action<TooltipTrigger, object>>(nameof(TooltipTrigger.SetObject))),
                };
                int startIndex = codes.FindCodes(findingCodes);
                if (startIndex >= 0)
                {
                    CodeInstruction[] patchingCodes = new CodeInstruction[]
                    {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld,
                        GetFieldInfo<InspectController, TooltipTrigger>("m_TooltipTrigger")),
                    new CodeInstruction(OpCodes.Ldc_I4_1),
                    new CodeInstruction(OpCodes.Callvirt,
                        GetMethodInfo<TooltipTrigger, Action<TooltipTrigger, bool>>(nameof(TooltipTrigger.ShowTooltipManual))),
                    };
                    return codes.InsertRange(startIndex + findingCodes.Length, patchingCodes, true).Complete();
                }
                else
                {
                    Core.FailedToPatch(MethodBase.GetCurrentMethod());
                    return codes;
                }
            }
        }
    }
}
