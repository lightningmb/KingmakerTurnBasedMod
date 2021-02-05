﻿using Kingmaker.Blueprints.Root;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.UnitLogic.Commands.Base;
using Kingmaker.UnitLogic.Parts;
using ModMaker.Utility;

namespace TurnBasedUpdated.Utility
{
    public static class UnitCommandExtensions
    {
        public static int GetAttackIndex(this UnitAttack command)
        {
            return command.GetFieldValue<UnitAttack, int>("m_AttackIndex");
        }
        
        public static void SetIsActed(this UnitCommand command, bool value)
        {
            command.SetPropertyValue(nameof(UnitCommand.IsActed), value);
        }

        public static void SetTimeSinceStart(this UnitCommand command, float value)
        {
            command.SetPropertyValue(nameof(UnitDoNothing.TimeSinceStart), value);
        }

        public static bool IsFullAttack(this UnitCommand command)
        {
            return command is UnitAttack unitAttack && unitAttack.IsFullAttack;
        }

        public static bool IsFullRoundAbility(this UnitCommand command)
        {
            return command is UnitUseAbility unitUseAbility && unitUseAbility.Spell.RequireFullRoundAction;
        }

        public static bool IsFullRoundAction(this UnitCommand command)
        {
            return command.IsFullAttack() || command.IsFullRoundAbility();
        }

        public static bool IsFreeTouch(this UnitCommand command)
        {
            if (command is UnitUseAbility unitUseAbility)
            {
                UnitPartTouch unitPartTouch = command.Executor.Get<UnitPartTouch>();
                if ((unitPartTouch?.IsCastedInThisRound ?? false) && unitUseAbility.Spell == unitPartTouch.Ability.Data)
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsSpellCombatAttack(this UnitCommand command)
        {
            return command is UnitAttack &&
                command.IsIgnoreCooldown &&
                command.Executor.Descriptor.HasFact(BlueprintRoot.Instance.SystemMechanics.MagusSpellCombatBuff);
        }

        public static bool IsSpellstrikeAttack(this UnitCommand command)
        {
            return command is UnitAttack &&
                command.IsIgnoreCooldown &&
                command.Executor.Descriptor.HasFact(BlueprintRoot.Instance.SystemMechanics.MagusSpellStrikeBuff);
        }

        public static bool IsActing(this UnitCommand command)
        {
            return command.IsActed && !command.IsFinished;
        }

        public static bool IsOffensiveCommand(this UnitCommand command)
        {
            UnitEntityData executor;
            UnitEntityData target;
            return command != null && !command.IsFinished && (command is UnitAttack || command is UnitUseAbility) &&
                (target = command.TargetUnit) != null && target != (executor = command.Executor) && executor.CanAttack(target);
        }
    }
}