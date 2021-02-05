﻿using Kingmaker.UI.SettingsUI;
using ModMaker.Utility;
using TurnBasedUpdated.Utility;
using UnityModManagerNet;

namespace TurnBasedUpdated
{
    public class Settings : UnityModManager.ModSettings
    {
        public string lastModVersion;
        public bool toggleTurnBasedUpdatedMode = true;

        // gameplay
        public float distanceOfFiveFootStep = 1.5f;
        public bool toggleSurpriseRound = true;
        public bool togglePreventUnconsciousUnitLeavingCombat = true;
        public bool toggleFlankingCountAllOpponentsWithinThreatenedRange = true;
        public bool toggleRerollPerceptionDiceAgainstStealthOncePerRound;

        public float radiusOfCollision = 0.9f;
        public bool toggleMovingThroughFriendlyUnit = true;
        public bool toggleMovingThroughNonHostileUnit;
        public bool toggleMovingThroughApplyToPlayer = true;
        public bool toggleMovingThroughApplyToNeutralUnit = true;
        public bool toggleMovingThroughApplyToEnemy = true;
        public bool toggleAvoidOverlapping = true;
        public bool toggleAvoidOverlappingOnCharge = true;
        public bool toggleDoNotMovingThroughNonAlly = true;

        public bool toggleAutoTurnOffAIOnTurnStart = true;
        public bool toggleAutoTurnOnAIOnCombatEnd = true;
        public bool toggleAutoSelectUnitOnTurnStart = true;
        public bool toggleAutoSelectEntirePartyOnCombatEnd = true;
        public bool toggleAutoCancelActionsOnTurnStart = true;
        public bool toggleAutoCancelActionsOnCombatEnd = true;
        public bool toggleAutoCancelActionsOnFiveFootStepFinish = true;
        public bool toggleAutoCancelActionsOnFirstMoveFinish = true;
        public bool toggleAutoEnableFiveFootStepOnTurnStart;
        public bool toggleAutoEndTurnWhenActionsAreUsedUp = true;
        public bool toggleAutoEndTurnExceptSwiftAction;
        public bool toggleAutoEndTurnWhenPlayerIdle;

        // interface
        public bool toggleDoNotMarkInvisibleUnit = true;
        public bool toggleDoNotShowInvisibleUnitOnCombatTracker = true;

        public float combatTrackerScale = 0.9f;
        public float combatTrackerWidth = 350f;
        public int combatTrackerMaxUnits = 15;
        public bool toggleCameraScrollToUnitOnClickUI = true;
        public bool toggleSelectUnitOnClickUI = true;
        public bool toggleInspectOnRightClickUI = true;
        public bool toggleShowIsFlatFootedIconOnUI = true;

        public bool toggleHighlightCurrentUnit = true;
        public bool toggleCameraScrollToCurrentUnit = true;
        public bool toggleCameraLockOnCurrentPlayerUnit;
        public bool toggleCameraLockOnCurrentNonPlayerUnit = true;

        public bool toggleShowAttackIndicatorOfCurrentUnit = true;
        public bool toggleShowAttackIndicatorForPlayer = true;
        public bool toggleShowAttackIndicatorForNonPlayer;
        public bool toggleShowAttackIndicatorOnHoverUI = true;
        public bool toggleShowAutoCastAbilityRange = true;
        public bool toggleCheckForObstaclesOnTargeting = true;

        public bool toggleShowMovementIndicatorOfCurrentUnit = true;
        public bool toggleShowMovementIndicatorForPlayer = true;
        public bool toggleShowMovementIndicatorForNonPlayer;
        public bool toggleShowMovementIndicatorOnHoverUI;

        // hotkeys
        public SerializableDictionary<string, BindingKeysData> hotkeys = new SerializableDictionary<string, BindingKeysData>();
        public bool hotkeyToggleFiveFootStepOnRightClickGround;

        // time scale
        public float timeScaleBetweenTurns = 5f;
        public float timeScaleInPlayerTurn = 1f;
        public float timeScaleInNonPlayerTurn = 2f;
        public float timeScaleInUnknownTurn = 3f;
        public float maxDelayBetweenIterativeAttacks = 1.5f;
        public float castingTimeOfFullRoundSpell = 0.5f;
        public float timeToWaitForIdleAI = 0.5f;
        public float timeToWaitForEndingTurn = 0.1f;

        // pause
        public bool toggleDoNotPauseOnCombatStart = true;
        public bool togglePauseOnPlayerTurnStart;
        public bool togglePauseOnPlayerTurnEnd;
        public bool togglePauseOnNonPlayerTurnStart;
        public bool togglePauseOnNonPlayerTurnEnd;
        public bool togglePauseOnPlayerFinishFiveFoot;
        public bool togglePauseOnPlayerFinishFirstMove;

        // bugfix
        public BugfixOption toggleFixNeverInCombatWithoutMC = new BugfixOption(true, false);
        public BugfixOption toggleFixActionTypeOfActivatingItem = new BugfixOption(true, false);
        public BugfixOption toggleFixActionTypeOfBardicPerformance = new BugfixOption(true, false);
        public BugfixOption toggleFixActionTypeOfSwappingWeapon = new BugfixOption(true, false);
        public BugfixOption toggleFixActionTypeOfCharge = new BugfixOption(true, false);
        public BugfixOption toggleFixActionTypeOfOverrun = new BugfixOption(true, false);
        public BugfixOption toggleFixActionTypeOfVitalStrike = new BugfixOption(true, false);
        public BugfixOption toggleFixActionTypeOfAngelicForm = new BugfixOption(true, false);
        public BugfixOption toggleFixActionTypeOfKineticBlade = new BugfixOption(true, false);
        public BugfixOption toggleFixKineticistWontStopPriorCommand = new BugfixOption(true, false);
        public BugfixOption toggleFixSpellstrikeOnNeutralUnit = new BugfixOption(true, false);
        public BugfixOption toggleFixSpellstrikeWithMetamagicReach = new BugfixOption(true, false);
        public BugfixOption toggleFixSpellstrikeWithNaturalWeapon = new BugfixOption(true, false);
        public BugfixOption toggleFixDamageBonusOfBlastRune = new BugfixOption(true, false);
        public BugfixOption toggleFixOnePlusDiv2ToDiv2 = new BugfixOption(true, false);
        public BugfixOption toggleFixFxOfShadowEvocationSirocco = new BugfixOption(true, false);
        public BugfixOption toggleFixAbilityNotAutoDeactivateIfCombatEnded = new BugfixOption(true, false);
        public BugfixOption toggleFixBlindFightDistance = new BugfixOption(true, false);
        public BugfixOption toggleFixDweomerLeap = new BugfixOption(true, false);
        public BugfixOption toggleFixConfusedUnitCanAttackDeadUnit = new BugfixOption(true, false);
        public BugfixOption toggleFixAcrobaticsMobility = new BugfixOption(true, false);
        public BugfixOption toggleFixCanMakeAttackOfOpportunityToUnmovedTarget = new BugfixOption(true, false);
        public BugfixOption toggleFixHasMotionThisTick = new BugfixOption(true, false);
        public BugfixOption toggleFixAbilityCircleRadius = new BugfixOption(true, false);
        public BugfixOption toggleFixAbilityCircleNotAppear = new BugfixOption(true, false);
        public BugfixOption toggleFixAbilityCanTargetUntargetableUnit = new BugfixOption(true, false);
        public BugfixOption toggleFixAbilityCanTargetDeadUnit = new BugfixOption(true, false);
        public BugfixOption toggleFixNeutralUnitCanAttackAlly = new BugfixOption(true, false);
        public BugfixOption toggleFixInspectingTriggerAuraEffect = new BugfixOption(true, false);
        public BugfixOption toggleFixInspectingCauseError = new BugfixOption(true, true);

        // localization
        public string localizationFileName;
    }
}
