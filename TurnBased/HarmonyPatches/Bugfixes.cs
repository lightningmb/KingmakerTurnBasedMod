﻿using DG.Tweening;
using Harmony12;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Items.Weapons;
using Kingmaker.Controllers;
using Kingmaker.Controllers.Clicks.Handlers;
using Kingmaker.Controllers.Combat;
using Kingmaker.Controllers.Units;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Items;
using Kingmaker.Items.Slots;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.ActivatableAbilities;
using Kingmaker.UnitLogic.Buffs.Components;
using Kingmaker.UnitLogic.Class.Kineticist;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.UnitLogic.Commands.Base;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.Utility;
using Kingmaker.View;
using Kingmaker.View.Equipment;
using Kingmaker.Visual.Decals;
using ModMaker.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TurnBasedUpdated.Utility;
using UnityEngine;
using static ModMaker.Utility.ReflectionCache;
using static TurnBasedUpdated.Main;
using static TurnBasedUpdated.Utility.SettingsWrapper;

namespace TurnBasedUpdated.HarmonyPatches
{
    static class Bugfixes
    {
        // fix when main character is not in the party, the game will never consider the player is in combat
        [HarmonyPatch(typeof(Player), nameof(Player.IsInCombat), MethodType.Getter)]
        static class Player_get_IsInCombat_Patch
        {
            [HarmonyPrefix]
            static bool Prefix(Player __instance, ref bool __result)
            {
                if (Mod.Enabled && FixNeverInCombatWithoutMC)
                {
                    __result = __instance.PartyCharacters.FirstOrDefault().Value?.Group.IsInCombat ?? false;
                    return false;
                }
                return true;
            }
        }

        // fix the action type of activating an item storing a full-round spell (e.g. summon monster scrolls)
        [HarmonyPatch(typeof(AbilityData), nameof(AbilityData.RequireFullRoundAction), MethodType.Getter)]
        static class AbilityData_RequireFullRoundAction_Patch
        {
            [HarmonyPostfix]
            static void Postfix(AbilityData __instance, ref bool __result)
            {
                if (Mod.Enabled && FixActionTypeOfActivatingItem && __result && __instance.SourceItem != null)
                {
                    __result = false;
                }
            }
        }

        // fix the action type of starting a Bardic Performance with/without Singing Steel
        [HarmonyPatch(typeof(UnitActivateAbility), "GetCommandType", typeof(ActivatableAbility))]
        static class UnitActivateAbility_GetCommandType_Patch
        {
            [HarmonyPrefix]
            static bool Prefix(ActivatableAbility ability, ref UnitCommand.CommandType __result)
            {
                if (Mod.Enabled && FixActionTypeOfBardicPerformance)
                {
                    if (ability.Blueprint.Group == ActivatableAbilityGroup.BardicPerformance)
                    {
                        if (ability.Owner.State.Features.QuickenPerformance2)
                        {
                            __result = ability.Owner.State.Features.SingingSteel ?
                                UnitCommand.CommandType.Free : UnitCommand.CommandType.Swift;
                        }
                        else if (ability.Owner.State.Features.QuickenPerformance1)
                        {
                            __result = ability.Owner.State.Features.SingingSteel ?
                                UnitCommand.CommandType.Swift : UnitCommand.CommandType.Move;
                        }
                        else
                        {
                            __result = ability.Owner.State.Features.SingingSteel ?
                                UnitCommand.CommandType.Move : UnitCommand.CommandType.Standard;
                        }
                    }
                    else
                    {
                        __result = ability.Blueprint.ActivateWithUnitCommandType;
                    }
                    return false;
                }
                return true;
            }
        }

        // fix the action type of swapping weapons
        //[HarmonyPatch]
        static class UnitViewHandsEquipment_AnimateEquipmentChangeInCombat_Patch
        {
            [HarmonyTargetMethod]
            static MethodBase TargetMethod(HarmonyInstance instance)
            {
                return typeof(UnitViewHandsEquipment)
                    .GetNestedTypes(BindingFlags.Instance | BindingFlags.NonPublic)
                    .First(type => type.Name.Contains("AnimateEquipmentChangeInCombat"))
                    .GetMethod(nameof(IEnumerator.MoveNext), BindingFlags.Instance | BindingFlags.Public);
            }

            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpiler(MethodBase original, IEnumerable<CodeInstruction> codes, ILGenerator il)
            {
                // ---------------- before 1 ----------------
                // $this.Owner.CombatState.HasCooldownForCommand(UnitCommand.CommandType.Standard)
                // ---------------- after  1 ----------------
                // HasCooldown($this.Owner.CombatState)
                // ---------------- before 2 ----------------
                // $this.Owner.CombatState.Cooldown.StandardAction = 6f;
                // ---------------- after  2 ----------------
                // UpdateCooldown($this.Owner.CombatState);
                CodeInstruction[] findingCodes_1 = new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Ldc_I4_1),
                    new CodeInstruction(OpCodes.Callvirt,
                        GetMethodInfo<UnitCombatState, Func<UnitCombatState, UnitCommand.CommandType, bool>>(nameof(UnitCombatState.HasCooldownForCommand)))
                };
                CodeInstruction[] findingCodes_2 = new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Ldfld,
                        GetFieldInfo<UnitCombatState, UnitCombatState.Cooldowns>(nameof(UnitCombatState.Cooldown))),
                    new CodeInstruction(OpCodes.Ldc_R4, 6f),
                    new CodeInstruction(OpCodes.Callvirt,
                        GetPropertyInfo<UnitCombatState.Cooldowns, float>(nameof(UnitCombatState.Cooldowns.StandardAction)).GetSetMethod(true))
                };
                int startIndex_1 = codes.FindCodes(findingCodes_1);
                int startIndex_2 = codes.FindCodes(findingCodes_2);
                if (startIndex_1 >= 0 && startIndex_2 >= 0)
                {
                    CodeInstruction[] patchingCodes_1 = new CodeInstruction[]
                    {
                        new CodeInstruction(OpCodes.Call,
                            new Func<UnitCombatState, bool>(HasCooldown).Method)
                    };
                    CodeInstruction[] patchingCodes_2 = new CodeInstruction[]
                    {
                        new CodeInstruction(OpCodes.Call,
                            new Action<UnitCombatState>(UpdateCooldown).Method)
                    };
                    return codes
                        .ReplaceRange(startIndex_2, findingCodes_2.Length, patchingCodes_2, true)
                        .ReplaceRange(startIndex_1, findingCodes_1.Length, patchingCodes_1, true)
                        .Complete();
                }
                else
                {
                    Core.FailedToPatch(MethodBase.GetCurrentMethod());
                    return codes;
                }
            }

            static bool HasCooldown(UnitCombatState combatState)
            {
                if (Mod.Enabled && FixActionTypeOfSwappingWeapon)
                {
                    return
                      combatState.HasCooldownForCommand(UnitCommand.CommandType.Standard) &&
                      combatState.HasCooldownForCommand(UnitCommand.CommandType.Move);
                }
                else
                {
                    return combatState.HasCooldownForCommand(UnitCommand.CommandType.Standard);
                }
            }

            static void UpdateCooldown(UnitCombatState combatState)
            {
                if (Mod.Enabled && FixActionTypeOfSwappingWeapon)
                {
                    if (combatState.HasCooldownForCommand(UnitCommand.CommandType.Move))
                        combatState.Cooldown.StandardAction = 6.0f;
                    else
                        combatState.Cooldown.MoveAction += 3.0f;
                }
                else
                {
                    combatState.Cooldown.StandardAction = 6.0f;
                }
            }
        }

        // fix activating Kinetic Blade is regarded as drawing weapon and costs an additional standard action
        [HarmonyPatch(typeof(UnitViewHandsEquipment), nameof(UnitViewHandsEquipment.HandleEquipmentSlotUpdated), typeof(HandSlot), typeof(ItemEntity))]
        static class UnitViewHandsEquipment_HandleEquipmentSlotUpdated_Patch
        {
            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpiler(MethodBase original, IEnumerable<CodeInstruction> codes, ILGenerator il)
            {
                // ---------------- before ----------------
                // ... InCombat ...
                // ---------------- after  ----------------
                // !DontCostAction(slot, previousItem) && InCombat
                CodeInstruction[] findingCodes = new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Call,
                        GetPropertyInfo<UnitViewHandsEquipment, bool>(nameof(UnitViewHandsEquipment.InCombat)).GetGetMethod(true)),
                    new CodeInstruction(OpCodes.Brfalse),
                };
                int startIndex = codes.FindCodes(findingCodes);
                if (startIndex >= 0)
                {
                    CodeInstruction[] patchingCodes = new CodeInstruction[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_1),
                        new CodeInstruction(OpCodes.Ldarg_2),
                        new CodeInstruction(OpCodes.Call,
                            new Func<HandSlot, ItemEntity, bool>(DontCostAction).Method),
                        new CodeInstruction(OpCodes.Brtrue, codes.Item(startIndex + 2).operand),
                    };
                    return codes.InsertRange(startIndex, patchingCodes, true).Complete();
                }
                else
                {
                    Core.FailedToPatch(MethodBase.GetCurrentMethod());
                    return codes;
                }
            }

            static bool DontCostAction(HandSlot slot, ItemEntity previousItem)
            {
                return Mod.Enabled && FixActionTypeOfKineticBlade &&
                    ((slot.MaybeItem.IsKineticBlast() && previousItem == null) ||
                    (slot.MaybeItem == null && previousItem.IsKineticBlast()));
            }
        }

        // fix Kineticist will not stop its previous action if you command it to attack with Kinetic Blade before combat
        [HarmonyPatch(typeof(KineticistController), "TryRunKineticBladeActivationAction")]
        static class KineticistController_TryRunKineticBladeActivationAction_Patch
        {
            [HarmonyPostfix]
            static void Postfix(UnitCommand cmd, ref UnitCommands.CustomHandlerData? customHandler)
            {
                if (Mod.Enabled && FixKineticistWontStopPriorCommand && 
                    customHandler.HasValue && (customHandler.Value.ExecuteBefore ?? cmd) != cmd)
                {
                    UnitCommands commands = cmd.Executor.Commands;

                    // remove conflicting command
                    UnitCommand prior = commands.Raw[(int)cmd.Type] ?? commands.GetPaired(cmd);
                    if (Game.Instance.IsPaused && commands.PreviousCommand == null && prior != null && prior.IsRunning)
                    {
                        commands.PreviousCommand = prior;
                        commands.PreviousCommand.SuppressAnimation();
                        commands.Raw[(int)commands.PreviousCommand.Type] = null;
                    }
                    else
                    {
                        commands.InterruptAndRemoveCommand(cmd.Type);
                    }

                    // update target
                    if (cmd.Type == UnitCommand.CommandType.Standard || commands.Standard == null)
                    {
                        commands.UpdateCombatTarget(cmd);
                    }
                }
            }
        }

        // fix Spellstrike does not take effect when attacking a neutral target
        [HarmonyPatch(typeof(UnitUseAbility), nameof(UnitUseAbility.CreateCastCommand))]
        static class UnitUseAbility_CreateCastCommand_Patch
        {
            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpiler(MethodBase original, IEnumerable<CodeInstruction> codes, ILGenerator il)
            {
                // ---------------- before ----------------
                // unit.IsEnemy(target.Unit)
                // ---------------- after  ----------------
                // IsTarget(unit, target.Unit)
                codes = codes
                    .ReplaceAll(
                        new CodeInstruction(OpCodes.Callvirt,
                            GetMethodInfo<UnitEntityData, Func<UnitEntityData, UnitEntityData, bool>>(nameof(UnitEntityData.IsEnemy))),
                        new CodeInstruction(OpCodes.Call,
                            new Func<UnitEntityData, UnitEntityData, bool>(IsTarget).Method),
                        out int replaced, true)
                    .Complete();
                if (replaced <= 0)
                {
                    Core.FailedToPatch(MethodBase.GetCurrentMethod());
                }
                return codes;
            }

            static bool IsTarget(UnitEntityData unit, UnitEntityData target)
            {
                return (Mod.Enabled && FixSpellstrikeOnNeutralUnit) ? unit.CanAttack(target) : unit.IsEnemy(target);
            }
        }

        // fix Spellstrike does not take effect when using Metamagic (Reach) on a touch spell
        [HarmonyPatch(typeof(AbilityData), nameof(AbilityData.IsRay), MethodType.Getter)]
        static class AbilityData_get_IsRay_Patch
        {
            [HarmonyPostfix]
            static void Postfix(AbilityData __instance, ref bool __result)
            {
                if (Mod.Enabled && FixSpellstrikeWithMetamagicReach && !__result)
                {
                    if (__instance.Blueprint.GetComponent<AbilityDeliverTouch>() != null &&
                        __instance.HasMetamagic(Metamagic.Reach))
                    {
                        __result = true;
                    }
                }
            }
        }

        // fix Spellstrike will apply touch effect twice when hitting target using natural weapon
        [HarmonyPatch(typeof(MagusController), nameof(MagusController.OnEventDidTrigger), typeof(RuleAttackWithWeapon))]
        static class MagusController_OnEventDidTrigger_RuleAttackWithWeapon_Patch
        {
            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpiler(MethodBase original, IEnumerable<CodeInstruction> codes, ILGenerator il)
            {
                // ---------------- before ----------------
                // evt.AttackRoll.Weapon.Blueprint.IsMelee
                // ---------------- after  ----------------
                // IsValidWeapon(evt.AttackRoll.Weapon.Blueprint)
                CodeInstruction[] findingCodes = new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Ldarg_1),
                    new CodeInstruction(OpCodes.Callvirt,
                        GetPropertyInfo<RuleAttackWithWeapon, RuleAttackRoll>(nameof(RuleAttackWithWeapon.AttackRoll)).GetGetMethod(true)),
                    new CodeInstruction(OpCodes.Callvirt,
                        GetPropertyInfo<RuleAttackRoll, ItemEntityWeapon>(nameof(RuleAttackRoll.Weapon)).GetGetMethod(true)),
                    new CodeInstruction(OpCodes.Callvirt,
                        GetPropertyInfo<ItemEntityWeapon, BlueprintItemWeapon>(nameof(ItemEntityWeapon.Blueprint)).GetGetMethod(true)),
                    new CodeInstruction(OpCodes.Callvirt,
                        GetPropertyInfo<BlueprintItemWeapon, bool>(nameof(BlueprintItemWeapon.IsMelee)).GetGetMethod(true))
                };
                int startIndex = codes.FindLastCodes(findingCodes);
                if (startIndex >= 0)
                {
                    return codes.Replace(startIndex + 4, new CodeInstruction(OpCodes.Call,
                        new Func<BlueprintItemWeapon, bool>(IsValidWeapon).Method), true).Complete();
                }
                else
                {
                    Core.FailedToPatch(MethodBase.GetCurrentMethod());
                    return codes;
                }
            }

            static bool IsValidWeapon(BlueprintItemWeapon weapon)
            {
                return (Mod.Enabled && FixSpellstrikeWithNaturalWeapon) ? weapon.IsMelee && !weapon.IsNatural : weapon.IsMelee;
            }
        }

        // fix Spellstrike does not take effect when using Metamagic (Reach) on a touch spell
        [HarmonyPatch(typeof(UnitPartMagus), nameof(UnitPartMagus.IsSpellFromMagusSpellList), typeof(AbilityData))]
        static class UnitPartMagus_IsSpellFromMagusSpellList_Patch
        {
            [HarmonyPostfix]
            static void Postfix(UnitPartMagus __instance, AbilityData spell, ref bool __result)
            {
                if (Mod.Enabled && FixSpellstrikeWithMetamagicReach && !__result)
                {
                    if (__instance.Spellbook.Blueprint.SpellList.SpellsByLevel.Any(list =>
                        list.Spells.Any(item => item.StickyTouch?.TouchDeliveryAbility == spell.Blueprint)))
                    {
                        __result = true;
                    }
                }
            }
        }

        // fix Blind-Fight needs a extreme close distance to prevent from losing AC instead of melee distance
        [HarmonyPatch(typeof(FlatFootedIgnore), nameof(FlatFootedIgnore.OnEventAboutToTrigger), typeof(RuleCheckTargetFlatFooted))]
        static class FlatFootedIgnore_OnEventAboutToTrigger_Patch
        {
            [HarmonyPostfix]
            static void Postfix(FlatFootedIgnore __instance, RuleCheckTargetFlatFooted evt)
            {
                if (Mod.Enabled && FixBlindFightDistance && !evt.IgnoreConcealment)
                {
                    if (evt.Target.Descriptor == __instance.Owner &&
                        __instance.Type == FlatFootedIgnoreType.BlindFight &&
                        evt.Initiator.CombatState.IsEngage(evt.Target))
                    {
                        evt.IgnoreConcealment = true;
                    }
                }
            }
        }

        // fix Dweomer Leap can be triggered by ally and always consumes no action (it should consume a swift action)
        [HarmonyPatch(typeof(DweomerLeapLogic), nameof(DweomerLeapLogic.OnTryToApplyAbilityEffect), typeof(AbilityExecutionContext), typeof(TargetWrapper))]
        static class DweomerLeapLogic_OnTryToApplyAbilityEffect_Patch
        {
            [HarmonyPrefix]
            static bool Prefix(AbilityExecutionContext context, TargetWrapper target)
            {
                if (Mod.Enabled && FixDweomerLeap)
                {
                    UnitEntityData caster;
                    return target.Unit != null && (caster = context.MaybeCaster) != null && target.Unit.CanAttack(caster);
                }
                return true;
            }

            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpiler(MethodBase original, IEnumerable<CodeInstruction> codes, ILGenerator il)
            {
                // ---------------- before ----------------
                // unitUseAbility.IgnoreCooldown();
                // ---------------- after  ----------------
                // if (!DontIgnoreCooldown())
                //     unitUseAbility.IgnoreCooldown();
                CodeInstruction[] findingCodes = new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Ldloc_2),
                    new CodeInstruction(OpCodes.Ldloca_S),
                    new CodeInstruction(OpCodes.Initobj, typeof(TimeSpan?)),
                    new CodeInstruction(OpCodes.Ldloc_3),
                    new CodeInstruction(OpCodes.Callvirt,
                        GetMethodInfo<UnitCommand, Action<UnitCommand, TimeSpan?>>(nameof(UnitCommand.IgnoreCooldown)))
                };
                int startIndex = codes.FindLastCodes(findingCodes);
                if (startIndex >= 0)
                {
                    CodeInstruction[] patchingCodes = new CodeInstruction[]
                    {
                        new CodeInstruction(OpCodes.Call,
                            new Func<bool>(DontIgnoreCooldown).Method),
                        new CodeInstruction(OpCodes.Brtrue, codes.NewLabel(startIndex + findingCodes.Length, il)),
                    };
                    return codes.InsertRange(startIndex, patchingCodes, true).Complete();
                }
                else
                {
                    Core.FailedToPatch(MethodBase.GetCurrentMethod());
                    return codes;
                }
            }

            static bool DontIgnoreCooldown()
            {
                return Mod.Enabled && FixDweomerLeap;
            }
        }

        // fix sometimes a confused unit can act normally because it tried but failed to attack a dead unit
        [HarmonyPatch(typeof(UnitConfusionController), "AttackNearest", typeof(UnitPartConfusion))]
        static class UnitConfusionController_AttackNearest_Patch
        {
            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpiler(MethodBase original, IEnumerable<CodeInstruction> codes, ILGenerator il)
            {
                // ---------------- before ----------------
                // unitInMemory != part.Owner.Unit
                // ---------------- after  ----------------
                // IsValid(unitInMemory) && unitInMemory != part.Owner.Unit
                // ---------------- before ----------------
                // unitInGroup != part.Owner.Unit
                // ---------------- after  ----------------
                // IsValid(unitInGroup) && unitInGroup != part.Owner.Unit
                CodeInstruction[] findingCodes = new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Callvirt,
                        GetPropertyInfo<UnitPart, UnitDescriptor>(nameof(UnitPart.Owner)).GetGetMethod(true)),
                    new CodeInstruction(OpCodes.Callvirt,
                        GetPropertyInfo<UnitDescriptor, UnitEntityData>(nameof(UnitDescriptor.Unit)).GetGetMethod(true)),
                    new CodeInstruction(OpCodes.Bne_Un)
                };
                int startIndex_1 = codes.FindCodes(findingCodes);
                int startIndex_2 = codes.FindCodes(startIndex_1 + findingCodes.Length, findingCodes);
                if (startIndex_1 >= 0 && startIndex_2 >= 0)
                {
                    return codes
                        .InsertRange(startIndex_2, CreatePatch(codes, il, startIndex_2), false)
                        .InsertRange(startIndex_1, CreatePatch(codes, il, startIndex_1), false).Complete();
                }
                else
                {
                    Core.FailedToPatch(MethodBase.GetCurrentMethod());
                    return codes;
                }
            }

            static CodeInstruction[] CreatePatch(IEnumerable<CodeInstruction> codes, ILGenerator il, int startIndex)
            {
                return new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Dup),
                    new CodeInstruction(OpCodes.Call,
                        new Func<UnitEntityData, bool>(IsValid).Method),
                    new CodeInstruction(OpCodes.Brtrue, codes.NewLabel(startIndex, il)),
                    new CodeInstruction(OpCodes.Pop),
                    new CodeInstruction(OpCodes.Br, codes.NewLabel(startIndex + 4, il))
                };
            }

            static bool IsValid(UnitEntityData unit)
            {
                return (Mod.Enabled && FixConfusedUnitCanAttackDeadUnit) ? !unit.Descriptor.State.IsDead : true;
            }
        }

        // fix you can make an AoO to an unmoved unit as if it's leaving the threatened range (when switching from reach weapon)
        // fix Acrobatics (Mobility) can be triggered even if the AoO is provoked due to reasons other than movement
        //[HarmonyPatch(typeof(UnitCombatState), "ShouldAttackOnDisengage", typeof(UnitEntityData))]
        static class UnitCombatState_ShouldAttackOnDisengage_Patch
        {
            [HarmonyPrefix]
            static bool Prefix(UnitEntityData target, ref bool __result)
            {
                if (Mod.Enabled && FixCanMakeAttackOfOpportunityToUnmovedTarget && !target.HasMotionThisTick)
                {
                    __result = false;
                    return false;
                }
                return true;
            }

            [HarmonyPostfix]
            static void Postfix(UnitCombatState __instance, UnitEntityData target, ref bool __result)
            {
                if (Mod.Enabled && FixAcrobaticsMobility && __result)
                {
                    if (target.Descriptor.State.HasCondition(UnitCondition.UseMobilityToNegateAttackOfOpportunity))
                    {
                        if (Rulebook.Trigger(new RuleSkillCheck(target, StatType.SkillMobility,
                            Rulebook.Trigger(new RuleCalculateCMD(target, __instance.Unit, CombatManeuver.None)).Result)).IsPassed)
                        {
                            __result = false;
                        }
                    }
                }
            }
        }

        // fix Acrobatics (Mobility) can be triggered even if the AoO is provoked due to reasons other than movement
        //[HarmonyPatch(typeof(UnitCombatState), nameof(UnitCombatState.AttackOfOpportunity), typeof(UnitEntityData))]
        static class UnitCombatState_AttackOfOpportunity_Patch
        {
            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpiler(MethodBase original, IEnumerable<CodeInstruction> codes, ILGenerator il)
            {
                // ---------------- before ----------------
                // target.Descriptor.State.HasCondition(UnitCondition.UseMobilityToNegateAttackOfOpportunity)
                // ---------------- after  ----------------
                // !ToFixAcrobaticsMobility() && target.Descriptor.State.HasCondition(UnitCondition.UseMobilityToNegateAttackOfOpportunity)
                CodeInstruction[] findingCodes = new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Ldloc_0),
                    new CodeInstruction(OpCodes.Ldfld),
                    new CodeInstruction(OpCodes.Callvirt,
                        GetPropertyInfo<UnitEntityData, UnitDescriptor>(nameof(UnitEntityData.Descriptor)).GetGetMethod(true)),
                    new CodeInstruction(OpCodes.Ldfld,
                        GetFieldInfo<UnitDescriptor, UnitState>(nameof(UnitDescriptor.State))),
                    new CodeInstruction(OpCodes.Ldc_I4_S, (sbyte) 36),
                    new CodeInstruction(OpCodes.Callvirt,
                        GetMethodInfo<UnitState, Func<UnitState, UnitCondition, bool>>(nameof(UnitState.HasCondition))),
                    new CodeInstruction(OpCodes.Brfalse),
                };
                int startIndex = codes.FindLastCodes(findingCodes);
                if (startIndex >= 0)
                {
                    CodeInstruction[] patchingCodes = new CodeInstruction[]
                    {
                        new CodeInstruction(OpCodes.Call,
                            new Func<bool>(ToFixAcrobaticsMobility).Method),
                        new CodeInstruction(OpCodes.Brtrue, codes.Item(startIndex + findingCodes.Length - 1).operand),
                    };
                    return codes.InsertRange(startIndex, patchingCodes, true).Complete();
                }
                else
                {
                    Core.FailedToPatch(MethodBase.GetCurrentMethod());
                    return codes;
                }
            }

            static bool ToFixAcrobaticsMobility()
            {
                return Mod.Enabled && FixAcrobaticsMobility;
            }
        }

        // fix sometimes the game does not regard a unit that is forced to move as a unit that is moved (cause AoO inconsistent)
        [HarmonyPatch(typeof(UnitMoveController), nameof(UnitMoveController.Tick))]
        static class UnitMoveController_Tick_Patch
        {
            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpiler(MethodBase original, IEnumerable<CodeInstruction> codes, ILGenerator il)
            {
                // ---------------- before ----------------
                // awakeUnit.PreviousPosition = awakeUnit.Position;
                // ---------------- after  ----------------
                // awakeUnit.PreviousPosition = GetPreviousPosition(awakeUnit);
                CodeInstruction[] findingCodes = new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Ldloc_1),
                    new CodeInstruction(OpCodes.Ldloc_1),
                    new CodeInstruction(OpCodes.Callvirt,
                        GetPropertyInfo<UnitEntityData, Vector3>(nameof(UnitEntityData.Position)).GetGetMethod(true)),
                    new CodeInstruction(OpCodes.Callvirt,
                        GetPropertyInfo<UnitEntityData, Vector3>(nameof(UnitEntityData.PreviousPosition)).GetSetMethod(true))
                };
                int startIndex = codes.FindCodes(findingCodes);
                if (startIndex >= 0)
                {
                    return codes.Replace(startIndex + 2, new CodeInstruction(OpCodes.Call,
                        new Func<UnitEntityData, Vector3>(GetPreviousPosition).Method), false).Complete();
                }
                else
                {
                    Core.FailedToPatch(MethodBase.GetCurrentMethod());
                    return codes;
                }
            }

            static Vector3 GetPreviousPosition(UnitEntityData awakeUnit)
            {
                return (Mod.Enabled && FixHasMotionThisTick) ?
                    awakeUnit.View?.transform.position ?? awakeUnit.Position : awakeUnit.Position;
            }
        }

        // fix the visual circle of certain abilities is inconsistent with the real range
        [HarmonyPatch(typeof(AbilityData), nameof(AbilityData.GetVisualDistance))]
        static class AbilityData_GetVisualDistance_Patch
        {
            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpiler(MethodBase original, IEnumerable<CodeInstruction> codes, ILGenerator il)
            {
                // ---------------- before ----------------
                // return Blueprint.GetRange(flag).Meters + corpulence + 0.5f;
                // ---------------- after  ----------------
                // return Blueprint.GetRange(flag).Meters + corpulence + GetValue();
                CodeInstruction[] findingCodes = new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Ldc_R4, 0.5f),
                    new CodeInstruction(OpCodes.Add),
                };
                int startIndex = codes.FindLastCodes(findingCodes);
                if (startIndex >= 0)
                {
                    return codes.Replace(startIndex, new CodeInstruction(OpCodes.Call,
                            new Func<float>(GetValue).Method), false).Complete();
                }
                else
                {
                    Core.FailedToPatch(MethodBase.GetCurrentMethod());
                    return codes;
                }
            }

            static float GetValue()
            {
                return Mod.Enabled && FixAbilityCircleRadius ? 0f : 0.5f;
            }
        }

        // fix the ability circle does not appear properly when you first time select any ability of the unit using the hotkey  
        //[HarmonyPatch(typeof(GUIDecal), "InitAnimator")]
        static class GUIDecal_InitAnimator_Patch
        {
            [HarmonyPostfix]
            static void Postfix(Tweener ___m_AppearAnimation, Tweener ___m_DisappearAnimation)
            {
                if (Mod.Enabled && FixAbilityCircleNotAppear)
                {
                    ___m_AppearAnimation.Pause();
                    ___m_DisappearAnimation.Pause();
                }
            }
        }

        // fix untargetable units can be targeted by abilities
        // fix dead units can be targeted by abilities that cannot be cast on dead targets
        [HarmonyPatch(typeof(ClickWithSelectedAbilityHandler), nameof(ClickWithSelectedAbilityHandler.GetPriority), typeof(GameObject), typeof(Vector3))]
        static class ClickWithSelectedAbilityHandler_GetPriority_Patch
        {
            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpiler(MethodBase original, IEnumerable<CodeInstruction> codes, ILGenerator il)
            {
                // ---------------- before ----------------
                // if (... Ability.CanTarget(target))
                // {
                //     ...
                // }
                // ---------------- after  ----------------
                // if (... Ability.CanTarget(target))
                // {
                //     if (CanNotTarget(Ability, target))
                //         return 0f;
                //     ...
                // }
                CodeInstruction[] findingCodes = new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Call,
                        GetPropertyInfo<ClickWithSelectedAbilityHandler, AbilityData>(nameof(ClickWithSelectedAbilityHandler.Ability)).GetGetMethod(true)),
                    new CodeInstruction(OpCodes.Ldloc_S),
                    new CodeInstruction(OpCodes.Callvirt,
                        GetMethodInfo<AbilityData, Func<AbilityData, TargetWrapper, bool>>(nameof(AbilityData.CanTarget))),
                    new CodeInstruction(OpCodes.Brfalse),
                };
                int startIndex = codes.FindCodes(findingCodes);
                if (startIndex >= 0)
                {
                    CodeInstruction[] patchingCodes = new CodeInstruction[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Call,
                            GetPropertyInfo<ClickWithSelectedAbilityHandler, AbilityData>(nameof(ClickWithSelectedAbilityHandler.Ability)).GetGetMethod(true)),
                        new CodeInstruction(OpCodes.Ldloc_S, codes.Item(startIndex + 2).operand),
                        new CodeInstruction(OpCodes.Call,
                            new Func<AbilityData, TargetWrapper, bool>(CanNotTarget).Method),
                        new CodeInstruction(OpCodes.Brfalse, codes.NewLabel(startIndex + findingCodes.Length, il)),
                        new CodeInstruction(OpCodes.Ldc_R4, 0f),
                        new CodeInstruction(OpCodes.Ret)
                    };
                    return codes.InsertRange(startIndex + findingCodes.Length, patchingCodes, true).Complete();
                }
                else
                {
                    Core.FailedToPatch(MethodBase.GetCurrentMethod());
                    return codes;
                }
            }

            static bool CanNotTarget(AbilityData ability, TargetWrapper target)
            {
                return Mod.Enabled && target.Unit != null &&
                    ((FixAbilityCanTargetUntargetableUnit && target.Unit.Descriptor.State.IsUntargetable) ||
                    (FixAbilityCanTargetDeadUnit && target.Unit.Descriptor.State.IsDead && !ability.Blueprint.CanCastToDeadTarget));
            }
        }

        // fix certain neutral units can attack their ally
        [HarmonyPatch(typeof(UnitEntityData), nameof(UnitEntityData.CanAttack), typeof(UnitEntityData))]
        static class UnitEntityData_CanAttack_Patch
        {
            [HarmonyPrefix]
            static bool Prefix(UnitEntityData __instance, UnitEntityData unit, ref bool __result)
            {
                if (Mod.Enabled && FixNeutralUnitCanAttackAlly)
                {
                    __result = (unit.Descriptor.Faction.Neutral && unit.Descriptor.Faction != __instance.Descriptor.Faction) ||
                        __instance.Group.IsEnemy(unit);
                    return false;
                }
                return true;
            }
        }

        // fix certain neutral units can attack their ally
        [HarmonyPatch]
        static class OffensiveActionsController_OnOffensiveActionsDidTrigger_Patch
        {
            [HarmonyTargetMethods]
            static IEnumerable<MethodBase> TargetMethods(HarmonyInstance instance)
            {
                yield return GetTargetMethod(typeof(OffensiveActionsController), nameof(OffensiveActionsController.OnEventDidTrigger));
                yield return GetTargetMethod(typeof(OffensiveActionsController), nameof(OffensiveActionsController.OnTryToApplyAbilityEffect));
            }

            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpiler(MethodBase original, IEnumerable<CodeInstruction> codes, ILGenerator il)
            {
                // ---------------- before 1 ----------------
                // initiator.IsEnemy(target)
                // ---------------- after  1 ----------------
                // IsTarget_1(initiator, target)
                // ---------------- before 2 ----------------
                // target.Faction.Neutral
                // ---------------- after  2 ----------------
                // IsTarget_2(target)
                codes = codes
                    .ReplaceAll(
                        new CodeInstruction(OpCodes.Callvirt,
                            GetMethodInfo<UnitEntityData, Func<UnitEntityData, UnitEntityData, bool>>(nameof(UnitEntityData.IsEnemy))),
                        new CodeInstruction(OpCodes.Call,
                            new Func<UnitEntityData, UnitEntityData, bool>(IsTarget_1).Method),
                        out int replaced_1, true)
                    .ReplaceAll(
                        new CodeInstruction[]
                        {
                            new CodeInstruction(OpCodes.Callvirt,
                                GetPropertyInfo<UnitEntityData, BlueprintFaction>(nameof(UnitEntityData.Faction)).GetGetMethod()),
                            new CodeInstruction(OpCodes.Ldfld,
                                GetFieldInfo<BlueprintFaction, bool>(nameof(BlueprintFaction.Neutral)))
                        },
                        new CodeInstruction[]
                        {
                            new CodeInstruction(OpCodes.Call,
                                new Func<UnitEntityData, bool>(IsTarget_2).Method),
                        },
                        out int replaced_2, true)
                    .Complete();
                if (replaced_1 <= 0 || replaced_2 <= 0)
                {
                    Core.FailedToPatch(MethodBase.GetCurrentMethod());
                }
                return codes;
            }

            static bool IsTarget_1(UnitEntityData initiator, UnitEntityData target)
            {
                return (Mod.Enabled && FixNeutralUnitCanAttackAlly) ? initiator.CanAttack(target) : initiator.IsEnemy(target);
            }

            static bool IsTarget_2(UnitEntityData target)
            {
                return (Mod.Enabled && FixNeutralUnitCanAttackAlly) ? false : target.Faction.Neutral;
            }

            static MethodBase GetTargetMethod(Type type, string name)
            {
                return type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
        }

        // fix certain aura effect will be triggered repeatedly when you inspect its owner
        [HarmonyPatch(typeof(AddAreaEffect), nameof(AddAreaEffect.OnFactActivate))]
        static class AddAreaEffect_OnFactActivate_Patch
        {
            [HarmonyPrefix]
            static bool Prefix(AddAreaEffect __instance)
            {
                if (Mod.Enabled && FixInspectingTriggerAuraEffect && !__instance.Owner.Unit.IsInGame)
                {
                    return false;
                }
                return true;
            }
        }

        // fix inspecting enemies can cause errors in certain condition
        [HarmonyPatch(typeof(AddAmbushBehaviour), nameof(AddAmbushBehaviour.OnEntityCreated), typeof(UnitEntityData))]
        static class AddAmbushBehaviour_OnEntityCreated_Patch
        {
            [HarmonyPrefix]
            static bool Prefix(UnitEntityData entity)
            {
                if (Mod.Enabled && FixInspectingCauseError && !entity.IsInGame)
                {
                    return false;
                }
                Mod.Log(entity);
                return true;
            }
        }

        // fix a native bug due to null View
        [HarmonyPatch]
        static class UnitEntityView_Corpulence_Patch
        {
            [HarmonyTargetMethods]
            static IEnumerable<MethodBase> TargetMethods(HarmonyInstance instance)
            {
                yield return GetTargetMethod(typeof(DweomerLeapLogic), nameof(DweomerLeapLogic.OnTryToApplyAbilityEffect));
                yield return GetTargetMethod(typeof(ContextActionMeleeAttack), nameof(ContextActionMeleeAttack.SelectTarget));
            }

            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpiler(MethodBase original, IEnumerable<CodeInstruction> codes, ILGenerator il)
            {
                // ---------------- before ----------------
                // .View.Corpulence
                // ---------------- after  ----------------
                // .Corpulence
                codes = codes
                    .ReplaceAll(new CodeInstruction[] 
                        {
                            new CodeInstruction(OpCodes.Callvirt,
                                GetPropertyInfo<UnitEntityData, UnitEntityView>(nameof(UnitEntityData.View)).GetGetMethod()),
                            new CodeInstruction(OpCodes.Callvirt,
                                GetPropertyInfo<UnitEntityView, float>(nameof(UnitEntityView.Corpulence)).GetGetMethod())
                        },
                        new CodeInstruction[]
                        {
                            new CodeInstruction(OpCodes.Callvirt,
                                GetPropertyInfo<UnitEntityData, float>(nameof(UnitEntityData.Corpulence)).GetGetMethod())
                        },
                        out int replaced, true)
                    .Complete();
                if (replaced <= 0)
                {
                    Core.FailedToPatch(MethodBase.GetCurrentMethod());
                }
                return codes;
            }

            static MethodBase GetTargetMethod(Type type, string name)
            {
                return type.GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            }
        }
    }
}
