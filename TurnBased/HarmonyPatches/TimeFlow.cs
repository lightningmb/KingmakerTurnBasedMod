﻿using Harmony12;
using Kingmaker;
using Kingmaker.AreaLogic.Cutscenes;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Root;
using Kingmaker.Controllers;
using Kingmaker.Controllers.Units;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.PubSubSystem;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities.Components.AreaEffects;
using Kingmaker.UnitLogic.Abilities.Components.Base;
using Kingmaker.UnitLogic.ActivatableAbilities;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.UnitLogic.Commands.Base;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.View;
using ModMaker.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TurnBased.Utility;
using static ModMaker.Utility.ReflectionCache;
using static TurnBased.Main;
using static TurnBased.Utility.SettingsWrapper;
using static TurnBased.Utility.StatusWrapper;

namespace TurnBased.HarmonyPatches
{
    static class TimeFlow
    {
        // control combat process
        [HarmonyPatch(typeof(Game), "Tick")]
        static class Game_Tick_Patch
        {
            [HarmonyPostfix]
            static void Postfix()
            {
                if (IsInCombat() && !Game.Instance.IsPaused)
                {
                    try
                    {
                        Mod.Core.Combat.Tick();
                    }
                    catch (Exception e)
                    {
                        Mod.Error(e);
                        Game.Instance.IsPaused = true;
                        EventBus.RaiseEvent<IWarningNotificationUIHandler>(h => h.HandleWarning(Local["UI_Txt_Error"], false));
                    }
                }
            }
        }

        // freeze game time during a unit's turn, and set the time scale
        [HarmonyPatch(typeof(TimeController), "Tick")]
        static class TimeController_Tick_Patch
        {
            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpiler(MethodBase original, IEnumerable<CodeInstruction> codes, ILGenerator il)
            {
                return codes.Patch(il, PreTranspiler, PostTranspiler);
            }

            static float? PreTranspiler()
            {
                if (IsInCombat() && !Game.Instance.IsPaused && !Game.Instance.InvertPauseButtonPressed)
                {
                    float playerTimeScale = Game.Instance.TimeController.PlayerTimeScale;
                    UnitEntityData unit = CurrentUnit();
                    Game.Instance.TimeController.PlayerTimeScale = playerTimeScale *
                        (unit == null ? TimeScaleBetweenTurns :
                        (!DoNotShowInvisibleUnitOnCombatTracker || unit.IsVisibleForPlayer) ?
                        (unit.IsDirectlyControllable ? TimeScaleInPlayerTurn : TimeScaleInNonPlayerTurn) : TimeScaleInUnknownTurn);
                    return playerTimeScale;
                }
                return null;
            }

            static void PostTranspiler(float? playerTimeScale)
            {
                if (playerTimeScale.HasValue)
                {
                    Game.Instance.TimeController.PlayerTimeScale = playerTimeScale.Value;
                }

                if (IsInCombat() && !Game.Instance.IsPaused)
                {
                    try
                    {
                        Mod.Core.Combat.TickTime();
                    }
                    catch (Exception e)
                    {
                        Mod.Error(e);
                        Game.Instance.IsPaused = true;
                        EventBus.RaiseEvent<IWarningNotificationUIHandler>(h => h.HandleWarning(Local["UI_Txt_Error"], false));
                    }
                }
            }
        }

        // block commands (e.g. stop non-'attack of opportunity' actions of non-current units), fix Magus spell combat
        [HarmonyPatch(typeof(UnitActionController), "TickCommand", typeof(UnitCommand), typeof(bool))]
        static class UnitActionController_TickCommand_Patch
        {
            [HarmonyPrefix]
            static bool Prefix(UnitCommand command)
            {
                if (IsInCombat())
                {
                    //                          passing     acting      ending      delaying
                    // not in combat            o           x           x           x
                    // in combat - not current  x           x           x           x
                    // in combat - current      x           o           o           x

                    bool canTick = default;

                    if (command is UnitAttackOfOpportunity)
                    {
                        canTick = true;
                    }
                    else if (command is UnitUseAbility unitUseImmediateAbility &&
                        unitUseImmediateAbility.Spell.Blueprint.ActionType == UnitCommand.CommandType.Swift &&
                        unitUseImmediateAbility.Spell.Blueprint.ComponentsArray.Any(c => c.name.StartsWith("$AbilityIsImmediateAction")))
                    {
                        canTick = true;
                    }
                    else if(IsPassing())
                    {
                        canTick = !command.Executor.IsInCombat;
                    }
                    else
                    {
                        canTick = command.Executor.IsCurrentUnit() && (IsActing() || IsEnding());

                        if (canTick && !command.IsStarted)
                        {
                            if (command.IsSpellCombatAttack() && !command.Executor.HasMoveAction())
                            {
                                command.Executor.Descriptor.RemoveFact(BlueprintRoot.Instance.SystemMechanics.MagusSpellCombatBuff);
                                command.Interrupt();
                                canTick = false;
                            }

                            if (command is UnitUseAbility unitUseAbility && !unitUseAbility.Spell.IsAvailableForCast)
                            {
                                command.Interrupt();
                                canTick = false;
                            }
                        }
                    }

                    if (!canTick)
                    {
                        return false;
                    }

                    if (command.Executor.IsInCombat)
                    {
                        command.NextApproachTime = Game.Instance.TimeController.GameTime;
                    }
                }
                return true;
            }
        }

        // block movement / ** make moving increase move action cooldown
        [HarmonyPatch(typeof(UnitMovementAgent), nameof(UnitMovementAgent.TickMovement), typeof(float))]
        static class UnitMovementAgent_TickMovement_Patch
        {
            [HarmonyPrefix]
            static bool Prefix(UnitMovementAgent __instance, ref float deltaTime)
            {
                if (IsInCombat())
                {
                    //                          passing     acting      ending      delaying
                    // not in combat            o           x           x           x
                    // in combat - not current  x           x           x           x
                    // in combat - current      x           o           x           x

                    bool canMove = default;
                    bool isInForceMode = __instance.GetIsInForceMode();
                    UnitEntityView view = __instance.Unit;

                    if (IsPassing())
                    {
                        canMove = !(view?.EntityData?.IsInCombat ?? false);
                    }
                    else
                    {
                        if (canMove = (view?.EntityData).IsCurrentUnit() && IsActing() &&
                            !(view.AnimationManager?.IsPreventingMovement ?? false) &&
                            !view.IsCommandsPreventMovement && __instance.IsReallyMoving)
                        {
                            CurrentTurn().TickMovement(ref deltaTime, isInForceMode);
                            canMove = deltaTime > 0f;
                        }
                        else
                        {
                            canMove = false;
                        }
                    }

                    if (!canMove)
                    {
                        if (!isInForceMode)
                        {
                            __instance.Stop();
                        }
                        return false;
                    }
                }
                return true;
            }

            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpiler(MethodBase original, IEnumerable<CodeInstruction> codes, ILGenerator il)
            {
                return codes.Patch<UnitMovementAgent, float?>(il, PreTranspiler, PostTranspiler);
            }

            static float? PreTranspiler(UnitMovementAgent instance)
            {
                if (IsInCombat())
                {
                    // disable acceleration effect
                    float minSpeed = instance.GetMinSpeed();
                    instance.SetMinSpeed(1f);
                    instance.SetWarmupTime(0f);
                    instance.SetSlowDownTime(0f);
                    return minSpeed;
                }
                return null;
            }

            static void PostTranspiler(UnitMovementAgent instance, float? minSpeed)
            {
                if (minSpeed.HasValue)
                {
                    instance.SetMinSpeed(minSpeed.Value);
                }
            }
        }

        // fix the exact range of movement is slightly shorter than the indicator range
        [HarmonyPatch(typeof(UnitMovementAgent), "SlowDown")]
        static class UnitMovementAgent_SlowDown_Patch
        {
            [HarmonyPrefix]
            static bool Prefix(UnitMovementAgent __instance)
            {
                if (IsInCombat() && (__instance.Unit?.EntityData?.IsInCombat ?? false))
                {
                    return false;
                }
                return true;
            }
        }

        // fix toggleable abilities
        [HarmonyPatch(typeof(UnitActivatableAbilitiesController), "TickOnUnit", typeof(UnitEntityData))]
        static class UnitActivatableAbilitiesController_TickOnUnit_Patch
        {
            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpiler(MethodBase original, IEnumerable<CodeInstruction> codes, ILGenerator il)
            {
                // ---------------- before ----------------
                // activatableAbility.TimeToNextRound -= Game.Instance.TimeController.GameDeltaTime;
                // if (activatableAbility.TimeToNextRound <= 0f)
                // ---------------- after  ----------------
                // activatableAbility.TimeToNextRound = GetTimeToNextRound(unit);
                // if (activatableAbility.TimeToNextRound <= 0f && CanTickNewRound(unit))
                CodeInstruction[] findingCodes = new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Ldloc_3),
                    new CodeInstruction(OpCodes.Dup),
                    new CodeInstruction(OpCodes.Callvirt,
                        GetPropertyInfo<ActivatableAbility, float>(nameof(ActivatableAbility.TimeToNextRound)).GetGetMethod(true)),
                    new CodeInstruction(OpCodes.Call,
                        GetPropertyInfo<Game, Game>(nameof(Game.Instance)).GetGetMethod(true)),
                    new CodeInstruction(OpCodes.Ldfld,
                        GetFieldInfo<Game, TimeController>(nameof(Game.TimeController))),
                    new CodeInstruction(OpCodes.Callvirt,
                        GetPropertyInfo<TimeController, float>(nameof(TimeController.GameDeltaTime)).GetGetMethod(true)),
                    new CodeInstruction(OpCodes.Sub),
                    new CodeInstruction(OpCodes.Callvirt,
                        GetPropertyInfo<ActivatableAbility, float>(nameof(ActivatableAbility.TimeToNextRound)).GetSetMethod(true)),
                    new CodeInstruction(OpCodes.Ldloc_3),
                    new CodeInstruction(OpCodes.Callvirt,
                        GetPropertyInfo<ActivatableAbility, float>(nameof(ActivatableAbility.TimeToNextRound)).GetGetMethod(true)),
                    new CodeInstruction(OpCodes.Ldc_R4),
                    new CodeInstruction(OpCodes.Bgt_Un),
                };
                int startIndex = codes.FindCodes(findingCodes);
                if (startIndex >= 0)
                {
                    CodeInstruction[] patchingCodes_1 = new CodeInstruction[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_1),
                        new CodeInstruction(OpCodes.Call,
                            new Func<float, UnitEntityData, float>(GetTimeToNextRound).Method)
                    };
                    CodeInstruction[] patchingCodes_2 = new CodeInstruction[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_1),
                        new CodeInstruction(OpCodes.Call,
                            new Func<UnitEntityData, bool>(CanTickNewRound).Method),
                        new CodeInstruction(OpCodes.Brfalse, codes.Item(startIndex + findingCodes.Length - 1).operand)
                    };
                    return codes
                        .InsertRange(startIndex + findingCodes.Length, patchingCodes_2, true)
                        .ReplaceRange(startIndex + 3, 4, patchingCodes_1, false).Complete();
                }
                else
                {
                    Core.FailedToPatch(MethodBase.GetCurrentMethod());
                    return codes;
                }
            }

            static float GetTimeToNextRound(float timeToNextRound, UnitEntityData unit)
            {
                if (IsInCombat())
                {
                    if (!IsPassing())
                    {
                        return timeToNextRound;
                    }
                    else if (unit.IsInCombat)
                    {
                        return unit.GetTimeToNextTurn();
                    }
                }
                return timeToNextRound -= Game.Instance.TimeController.GameDeltaTime;
            }

            static bool CanTickNewRound(UnitEntityData unit)
            {
                return !IsInCombat() || !unit.IsInCombat || (unit.IsCurrentUnit() && (IsActing() || IsEnding()));
            }
        }

        // tick ray effects even while time is frozen (e.g. Lightning Bolt)
        [HarmonyPatch(typeof(RayView), nameof(RayView.Update))]
        static class RayView_Update_Patch
        {
            [HarmonyPrefix]
            static void Prefix(RayView __instance)
            {
                if (IsInCombat() && !IsPassing())
                {
                    if (Mod.Core.Combat.TickedRayView.Add(__instance))
                    {
                        __instance.SetPrevTickTime(TimeSpan.Zero);
                    }
                }
            }
        }

        // tick AbilityDeliverEffect even while time is frozen (e.g. Scorching Ray)
        [HarmonyPatch(typeof(AbilityExecutionProcess), nameof(AbilityExecutionProcess.Tick))]
        static class AbilityExecutionProcess_Tick_Patch
        {
            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpiler(MethodBase original, IEnumerable<CodeInstruction> codes, ILGenerator il)
            {
                return codes.Patch<AbilityExecutionProcess, TimeSpan?>(il, PreTranspiler, PostTranspiler);
            }

            static TimeSpan? PreTranspiler(AbilityExecutionProcess instance)
            {
                if ((IsInCombat() && instance.Context.AbilityBlueprint.GetComponent<AbilityDeliverEffect>() != null) ||
                    (Mod.Enabled && Mod.Core.LastTickTimeOfAbilityExecutionProcess.ContainsKey(instance)))
                {
                    TimeSpan gameTime = Game.Instance.Player.GameTime;
                    TimeSpan newGameTime = Mod.Core.LastTickTimeOfAbilityExecutionProcess.TryGetValue(instance, out newGameTime) ?
                        newGameTime + Game.Instance.TimeController.GameDeltaTime.Seconds() : gameTime;
                    Mod.Core.LastTickTimeOfAbilityExecutionProcess[instance] = newGameTime;
                    Game.Instance.Player.GameTime = newGameTime;
                    return gameTime;
                }
                return null;
            }

            static void PostTranspiler(AbilityExecutionProcess instance, TimeSpan? gameTime)
            {
                if (gameTime.HasValue)
                {
                    if (instance.IsEnded)
                        Mod.Core.LastTickTimeOfAbilityExecutionProcess.Remove(instance);
                    Game.Instance.Player.GameTime = gameTime.Value;
                }
            }

            static MethodBase GetTargetMethod(Type type, string name)
            {
                return type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
        }

        // stop ticking AbilityAreaEffectMovement when time is frozen (e.g. Cloudkill)
        [HarmonyPatch(typeof(AbilityAreaEffectMovement), "OnTick", typeof(MechanicsContext), typeof(AreaEffectEntityData))]
        static class AbilityAreaEffectMovement_OnTick_Patch
        {
            [HarmonyPrefix]
            static bool Prefix()
            {
                return !IsInCombat() || IsPassing();
            }
        }

        // stop ticking cutscene commands when time is frozen (to fix some scripted event animations being skipped during TB combat)
        [HarmonyPatch(typeof(CutscenePlayerData.TrackData), nameof(CutscenePlayerData.TrackData.Tick), typeof(CutscenePlayerData))]
        static class TrackData_Tick_Patch
        {
            [HarmonyPrefix]
            static bool Prefix(CutscenePlayerData.TrackData __instance)
            {
                return !IsInCombat() || IsPassing() || !__instance.IsPlaying;
            }
        }

        // ** moved to ModTurnController
        [HarmonyPatch(typeof(UnitTicksController), "TickOnUnit", typeof(UnitEntityData))]
        static class UnitTicksController_TickOnUnit_Patch
        {
            [HarmonyPrefix]
            static bool Prefix(UnitEntityData unit)
            {
                return !IsInCombat() || !unit.IsInCombat;
            }
        }

        // stop time advancing during units' turn
        [HarmonyPatch]
        static class BaseUnitController_TickDeltaTime_Patch
        {
            [HarmonyTargetMethods]
            static IEnumerable<MethodBase> TargetMethods(HarmonyInstance instance)
            {
                yield return GetTargetMethod(typeof(UnitInPitController), "TickOnUnit");
                yield return GetTargetMethod(typeof(UnitsProximityController), "TickOnUnit");
            }

            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpiler(MethodBase original, IEnumerable<CodeInstruction> codes, ILGenerator il)
            {
                return codes.Patch(il, PreTranspiler, PostTranspiler);
            }

            static float? PreTranspiler()
            {
                if (IsInCombat() && !IsPassing())
                {
                    float deltaTime = Game.Instance.TimeController.DeltaTime;
                    Game.Instance.TimeController.SetDeltaTime(0f);
                    return deltaTime;
                }
                return null;
            }

            static void PostTranspiler(float? deltaTime)
            {
                if (deltaTime.HasValue)
                {
                    Game.Instance.TimeController.SetDeltaTime(deltaTime.Value);
                }
            }

            static MethodBase GetTargetMethod(Type type, string name)
            {
                return type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
        }

        // stop time advancing during units' turn
        [HarmonyPatch]
        static class BaseUnitController_TickGameDeltaTime_Patch
        {
            [HarmonyTargetMethods]
            static IEnumerable<MethodBase> TargetMethods(HarmonyInstance instance)
            {
                yield return GetTargetMethod(typeof(UnitFearController), "TickOnUnit");
                yield return GetTargetMethod(typeof(UnitStealthController), "Tick");
                yield return GetTargetMethod(typeof(UnitSwallowWholeController), "TickOnUnit");
                yield return GetTargetMethod(typeof(AreaEffectEntityData), "Tick");
            }

            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpiler(MethodBase original, IEnumerable<CodeInstruction> codes, ILGenerator il)
            {
                return codes.Patch(il, PreTranspiler, PostTranspiler);
            }

            static float? PreTranspiler()
            {
                if (IsInCombat() && !IsPassing())
                {
                    float gameDeltaTime = Game.Instance.TimeController.GameDeltaTime;
                    Game.Instance.TimeController.SetGameDeltaTime(0f);
                    return gameDeltaTime;
                }
                return null;
            }

            static void PostTranspiler(float? gameDeltaTime)
            {
                if (gameDeltaTime.HasValue)
                {
                    Game.Instance.TimeController.SetGameDeltaTime(gameDeltaTime.Value);
                }
            }

            static MethodBase GetTargetMethod(Type type, string name)
            {
                return type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
        }

        // stop time advanced during units' turn
        // ok - it's good
        // oo - no matter
        // ss - in special patch
        // ?? - not sure
        // xx - don't touch
        // UnitCombatCooldownsController        // ss - GameDeltaTime
        // UnitActivatableAbilitiesController   // ss - GameDeltaTime
        // SummonedUnitsController              // xx
        // UnitAnimationController              // xx
        // UnitBuffsController                  // oo - GameTime
        // UnitConfusionController              // ss - GameTime
        // UnitForceMoveController              // xx
        // UnitGrappleController                // oo - GameTime
        // UnitGuardController                  // xx
        // UnitLifeController                   // xx
        // UnitMimicController                  // xx
        // UnitProneController                  // ss - DeltaTime
        // UnitRoamingController                // ?? - GameTime
        // UnitsProximityController             // ?? - DeltaTime
        // UnitTicksController                  // ss - GameDeltaTime
        // UnitStealthController                // ss - GameTime + GameDeltaTime
    }
}
