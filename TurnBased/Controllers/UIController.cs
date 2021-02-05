﻿using Kingmaker;
using Kingmaker.PubSubSystem;
using ModMaker;
using ModMaker.Utility;
using System.Reflection;
using TurnBasedUpdated.UI;
using UnityEngine;
using static TurnBasedUpdated.Main;

namespace TurnBasedUpdated.Controllers
{
    public class UIController :
        IModEventHandler,
        ISceneHandler
    {
        public CombatTrackerManager CombatTracker { get; private set; }

        public AttackIndicatorManager AttackIndicator { get; private set; }

        public MovementIndicatorManager MovementIndicator { get; private set; }

        public int Priority => 800;

        public void Attach()
        {
            if (!CombatTracker)
            {
                CombatTracker = CombatTrackerManager.CreateObject();
            }

            if (!AttackIndicator)
            {
                AttackIndicator = AttackIndicatorManager.CreateObject();
            }

            if (!MovementIndicator)
            {
                MovementIndicator = MovementIndicatorManager.CreateObject();
            }
        }

        public void Detach()
        {
            CombatTracker.SafeDestroy();
            CombatTracker = null;

            AttackIndicator.SafeDestroy();
            AttackIndicator = null;

            MovementIndicator.SafeDestroy();
            MovementIndicator = null;
        }

#if DEBUG
        public void Clear()
        {
            Transform combatTracker;
            while (combatTracker = Game.Instance.UI.Common.transform.Find("HUDLayout/TurnBasedUpdatedCombatTracker"))
            {
                combatTracker.SafeDestroy();
            }
            CombatTracker = null;

            Transform attackIndicator;
            while (attackIndicator = Game.Instance.UI.Common.transform.Find("AbilityTargetSelect/TurnBasedUpdatedAttackIndicator"))
            {
                attackIndicator.gameObject.SafeDestroy();
            }
            AttackIndicator = null;

            Transform movementIndicator;
            while (movementIndicator = Game.Instance.UI.Common.transform.Find("AbilityTargetSelect/TurnBasedUpdatedMovementIndicator"))
            {
                movementIndicator.SafeDestroy();
            }
            MovementIndicator = null;
        }
#endif

        public void Update()
        {
            Detach();
            Attach();
        }

        #region Event Handlers

        public void HandleModEnable()
        {
            Mod.Debug(MethodBase.GetCurrentMethod());

            Mod.Core.UI = this;
            Attach();

            EventBus.Subscribe(this);
        }

        public void HandleModDisable()
        {
            Mod.Debug(MethodBase.GetCurrentMethod());

            EventBus.Unsubscribe(this);

            Detach();
            Mod.Core.UI = null;
        }

        public void OnAreaBeginUnloading() { }

        public void OnAreaDidLoad()
        {
            Mod.Debug(MethodBase.GetCurrentMethod());

            Attach();
        }

        #endregion
    }
}