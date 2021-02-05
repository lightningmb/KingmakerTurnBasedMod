﻿using Harmony12;
using Kingmaker;
using Kingmaker.Controllers.Units;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.UnitLogic.Parts;
using System;
using TurnBasedUpdated.Utility;
using static TurnBasedUpdated.Utility.StatusWrapper;

namespace TurnBasedUpdated.HarmonyPatches
{
    static class Confusion
    {
        // decide the action of Confusion right after current unit's turn start
        [HarmonyPatch(typeof(UnitConfusionController), "TickOnUnit", typeof(UnitEntityData))]
        static class UnitConfusionController_TickOnUnit_Patch
        {
            [HarmonyPrefix, HarmonyPriority(Priority.HigherThanNormal)]
            static bool Prefix(UnitEntityData unit)
            {
                if (IsInCombat())
                {
                    if (unit.IsCurrentUnit())
                    {
                        UnitPartConfusion unitPartConfusion = unit.Get<UnitPartConfusion>();
                        if (unitPartConfusion && unitPartConfusion.RoundStartTime < Game.Instance.TimeController.GameTime)
                        {
                            unitPartConfusion.Cmd?.Interrupt();
                            unitPartConfusion.RoundStartTime = TimeSpan.Zero;
                        }
                    }
                    else
                    {
                        return IsPassing() && !unit.IsInCombat;
                    }
                }
                return true;
            }
        }

        // make current unit end turn after do-nothing
        [HarmonyPatch(typeof(UnitDoNothing), "OnTick")]
        static class UnitDoNothing_OnTick_Patch
        {
            [HarmonyPrefix]
            static void Prefix(UnitDoNothing __instance)
            {
                if (IsInCombat() && __instance.Executor.IsCurrentUnit())
                {
                    __instance.SetTimeSinceStart(6f);
                    CurrentTurn().ForceToEnd();
                }
            }
        }

        // make current unit end turn after self-harm
        [HarmonyPatch(typeof(UnitSelfHarm), "OnAction")]
        static class UnitSelfHarm_OnAction_Patch
        {
            [HarmonyPostfix]
            static void Postfix(UnitSelfHarm __instance)
            {
                if (IsInCombat() && __instance.Executor.IsCurrentUnit())
                {
                    CurrentTurn().ForceToEnd();
                }
            }
        }
    }
}