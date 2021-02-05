﻿using Kingmaker;
using Kingmaker.Blueprints.Root;
using Kingmaker.Controllers;
using Kingmaker.Controllers.Combat;
using Kingmaker.EntitySystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.UI.Log;
using Kingmaker.UnitLogic;
using Kingmaker.View;
using ModMaker;
using ModMaker.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TurnBasedUpdated.Utility;
using UnityEngine;
using static TurnBasedUpdated.Main;
using static TurnBasedUpdated.Utility.SettingsWrapper;

namespace TurnBasedUpdated.Controllers
{
    public class ModCombatController : 
        IModEventHandler,
        IInGameHandler,
        IPartyCombatHandler,
        ISceneHandler,
        IUnitCombatHandler,
        IUnitHandler,
        IUnitInitiativeHandler
    {
        #region Fields

        private ModTurnController _currentTurn;
        private bool _hasEnemyInCombat;
        private bool _hasSurpriseRound;
        private bool _isUnitsChanged;
        private TimeSpan _startTime;
        private List<UnitEntityData> _units = new List<UnitEntityData>();
        HashSet<UnitEntityData> _unitsToSurprise = new HashSet<UnitEntityData>();
        private readonly UnitsOrderComaprer _unitsOrderComaprer = new UnitsOrderComaprer();

        #endregion

        #region Properties

        public ModTurnController CurrentTurn {
            get => _currentTurn;
            private set {
                if (_currentTurn != value)
                {
                    _currentTurn?.Dispose();
                    _currentTurn = value;
                }
            }
        }

        public bool HasEnemyInCombat {
            get {
                UpdateUnitsInfo();
                return _hasEnemyInCombat;
            }
            set => _hasEnemyInCombat = value;
        }

        public bool Initialized { get; private set; }

        public int Priority => 600;

        public IEnumerable<UnitEntityData> SortedUnits {
            get {
                UpdateUnitsInfo();
                return _units;
            }
        }

        public int RoundNumber { get; private set; }

        internal HashSet<RayView> TickedRayView { get; } = new HashSet<RayView>();

        public float TimeSinceStart { get; private set; }

        public float TimeToNextRound { get; private set; }

        #endregion

        #region Tick

        internal void Tick()
        {
            // advance the turn status
            CurrentTurn?.Tick();

            // try to start a turn for the next unit
            if (CurrentTurn == null)
            {
                UnitEntityData nextUnit = SortedUnits.FirstOrDefault();
                if (nextUnit != null && nextUnit.GetTimeToNextTurn() <= 0f)
                {
                    StartTurn(nextUnit);
                }
            }

            // reset parameters of the pervious tick
            TickedRayView.Clear();
        }

        internal void TickTime()
        {
            if (CurrentTurn == null)
            {
                // trim the delta time, when a turn will start at the end of this tick
                TimeController timeController = Game.Instance.TimeController;
                float timeToNextTurn = SortedUnits.FirstOrDefault()?.GetTimeToNextTurn() ?? 0f;
                if (timeController.GameDeltaTime > timeToNextTurn && timeToNextTurn != 0f)
                {
                    timeController.SetDeltaTime(timeToNextTurn);
                    timeController.SetGameDeltaTime(timeToNextTurn);
                }

                // advance time
                TimeSinceStart += timeController.GameDeltaTime;
                if ((TimeToNextRound -= timeController.GameDeltaTime) <= 0f)
                {
                    RoundNumber++;
                    TimeToNextRound += 6f;
                    LogRound();
                }
            }

            // set game time
            Game.Instance.Player.GameTime = _startTime + TimeSinceStart.Seconds();
        }

        #endregion

        #region Methods

        public bool IsSurprising(UnitEntityData unit)
        {
            return _hasSurpriseRound && TimeSinceStart < 6f && 
                (unit == CurrentTurn?.Unit ? true : unit.GetTimeToNextTurn() < TimeToNextRound);
        }

        public void StartTurn(UnitEntityData unit)
        {
            if (unit.IsInCombat && _units.Contains(unit))
            {
                CurrentTurn = new ModTurnController(unit);
                CurrentTurn.OnDelay += HandleDelayTurn;
                CurrentTurn.OnEnd += HandleEndTurn;
                CurrentTurn.Start();
                _isUnitsChanged = true;
            }
        }

        public void Reset(bool tryToInitialize, bool isPartyCombatStateChanged = false)
        {
            Mod.Debug(MethodBase.GetCurrentMethod(), tryToInitialize, isPartyCombatStateChanged);

            // try to initialize
            if (tryToInitialize && Mod.Core.Enabled && Game.Instance.Player.IsInCombat)
                HandleCombatStart(isPartyCombatStateChanged);
            else if(Initialized)
                HandleCombatEnd();
        }

        private void Clear()
        {
            // reset fields and properties
            _hasSurpriseRound = false;
            _isUnitsChanged = false;
            _startTime = Game.Instance.Player.GameTime;
            _units.Clear();
            _unitsToSurprise.Clear();
            CurrentTurn = null;
            HasEnemyInCombat = false;
            Initialized = false;
            RoundNumber = 0;
            TickedRayView.Clear();
            TimeSinceStart = 0f;
            TimeToNextRound = 0f;
        }

        private void HandleCombatStart(bool isPartyCombatStateChanged)
        {
            Clear();

            _units.AddRange(Game.Instance.State.Units.Where(unit => unit.IsInCombat));
            _isUnitsChanged = true;

            // surprise round
            if (isPartyCombatStateChanged && SurpriseRound)
            {
                HashSet<UnitEntityData> playerUnits = new HashSet<UnitEntityData>(Game.Instance.Player.ControllableCharacters);
                int notAppearUnitsCount = 0;
                bool isInitiatedByPlayer = _units.Any(unit => playerUnits.Contains(unit) && unit.HasOffensiveCommand());

                // try to join units to the surprise round
                foreach (UnitEntityData unit in _units)
                {
                    if (unit.Descriptor.HasFact(BlueprintRoot.Instance.SystemMechanics.SummonedUnitAppearBuff))
                        // this unit is just summoned by a full round spell and technically it does not exist on combat start
                        notAppearUnitsCount++;
                    else if (unit.IsSummoned(out UnitEntityData caster) && _unitsToSurprise.Contains(caster))
                        // this summoned unit will act after its caster's turn
                        _unitsToSurprise.Add(unit);
                    else if (
                        // player
                        playerUnits.Contains(unit) ?
                        isInitiatedByPlayer && unit.IsUnseen() :
                        // enemy
                        unit.Group.IsEnemy(Game.Instance.Player.Group) ?
                        unit.HasOffensiveCommand(command => playerUnits.Contains(command.TargetUnit)) ||
                        (unit.IsUnseen() && !unit.IsVisibleForPlayer) :
                        // neutral
                        unit.IsUnseen())
                        // this unit will act on its initiative
                        _unitsToSurprise.Add(unit);
                }

                // determine whether the surprise round occurs 
                if (_unitsToSurprise.Count > 0)
                {
                    if (_unitsToSurprise.Count < _units.Count - notAppearUnitsCount)
                        _hasSurpriseRound = true;
                    else
                        _unitsToSurprise.Clear();
                }
            }

            RoundNumber = _hasSurpriseRound ? 0 : 1;
            TimeToNextRound = 6f;
            LogRound();

            Initialized = true;
        }

        private void HandleCombatEnd()
        {
            Clear();

            // QoLs - on turn-based combat end
            if (AutoTurnOnAIOnCombatEnd)
                foreach (UnitEntityData unit in Game.Instance.Player.ControllableCharacters)
                    unit.IsAIEnabled = true;

            if (AutoSelectEntirePartyOnCombatEnd)
                Game.Instance.UI.SelectionManager?.SelectAll();

            if (AutoCancelActionsOnCombatEnd)
                foreach (UnitEntityData unit in Game.Instance.Player.ControllableCharacters)
                    unit.TryCancelCommands();
        }

        private void LogRound()
        {
            Game.Instance.UI.BattleLogManager.LogView.AddLogEntry(
                (RoundNumber > 0 ? string.Format(Local["UI_Log_RoundStarted"], RoundNumber) : Local["UI_Log_SurpriseRoundStarted"]).Bold(),
                new Color(0.5f, 0.1f, 0.1f, 1f), LogChannel.Combat);
        }

        private void AddUnit(UnitEntityData unit)
        {
            if (unit.IsInCombat && !_units.Contains(unit))
            {
                _units.Add(unit);
                _isUnitsChanged = true;
            }
        }

        private void InsertUnit(UnitEntityData unit, UnitEntityData targetUnit)
        {
            if (unit.IsInCombat && !_units.Contains(unit))
            {
                _units.Insert(_units.IndexOf(targetUnit) + 1, unit);
                _isUnitsChanged = true;
            }
        }

        private void RemoveUnit(UnitEntityData unit)
        {
            if (_units.Remove(unit))
            {
                if (CurrentTurn?.Unit == unit)
                {
                    CurrentTurn = null;
                }
                _isUnitsChanged = true;
            }
        }

        private void UpdateUnitsInfo()
        {
            if (_isUnitsChanged)
            {
                HasEnemyInCombat = Game.Instance.Player.Group.HasEnemyInCombat();
                _units = _units.OrderBy(unit => unit, _unitsOrderComaprer).ToList();    // stable sort
                _isUnitsChanged = false;

                // fix if combat is ended by a cutscene, HandlePartyCombatStateChanged will not be triggered
                if (_units.Count == 0 || !Game.Instance.Player.IsInCombat)
                {
                    foreach (UnitEntityData allCharacter in Game.Instance.Player.AllCharacters)
                    {
                        allCharacter.Buffs.OnCombatEnded();
                    }
                    EventBus.RaiseEvent<IPartyCombatHandler>(h => h.HandlePartyCombatStateChanged(false));
                }
            }
        }

        #endregion

        #region Event Handlers

        private void HandleDelayTurn(UnitEntityData unit, UnitEntityData targetUnit)
        {
            if (unit != targetUnit)
            {
                if (targetUnit.GetTimeToNextTurn() >= TimeToNextRound)
                {
                    CurrentTurn.ForceTickActivatableAbilities();
                }
                RemoveUnit(unit);
                InsertUnit(unit, targetUnit);
            }
        }

        private void HandleEndTurn(UnitEntityData unit)
        {
            RemoveUnit(unit);
            AddUnit(unit);
        }

        public void HandleModEnable()
        {
            Mod.Debug(MethodBase.GetCurrentMethod());

            Mod.Core.Combat = this;
            Reset(true);

            EventBus.Subscribe(this);
        }

        public void HandleModDisable()
        {
            Mod.Debug(MethodBase.GetCurrentMethod());

            EventBus.Unsubscribe(this);

            Reset(false);
            Mod.Core.Combat = null;
        }

        public void OnAreaBeginUnloading() { }

        public void OnAreaDidLoad()
        {
            Mod.Debug(MethodBase.GetCurrentMethod());

            Reset(false);
        }

        public void HandlePartyCombatStateChanged(bool inCombat)
        {
            Mod.Debug(MethodBase.GetCurrentMethod(), inCombat);

            Reset(inCombat, true);
        }

        public void HandleUnitRollsInitiative(RuleInitiativeRoll rule)
        {
            UnitEntityData unit = rule.Initiator;
            UnitCombatState.Cooldowns cooldown = unit.CombatState.Cooldown;
            if (TimeSinceStart == 0f)
            {
                // it's the beginning of combat
                if (unit.IsSummoned(out UnitEntityData caster) && _units.Contains(caster))
                {
                    // this unit is summoned before the combat, it will act right after its caster
                    cooldown.Initiative = caster.CombatState.Cooldown.Initiative;
                }
                else if (_hasSurpriseRound && !_unitsToSurprise.Contains(unit))
                {
                    // this unit is surprised, it will be flat-footed for one more round
                    cooldown.Initiative += 6f;
                }

                _unitsToSurprise.Remove(unit);
            }
            else
            {
                // it's the middle of combat
                if (unit.IsSummoned(out UnitEntityData caster) && _units.Contains(caster))
                {
                    // summoned units can act instantly, it's delay is controlled by its buff
                    cooldown.Initiative = 0f;

                    // ensure its order is right after its caster
                    RemoveUnit(unit);
                    InsertUnit(unit, caster);
                }
                else
                {
                    if (_hasSurpriseRound && TimeSinceStart < 6f)
                    {
                        // units that join during surprise round will be regard as surprised
                        cooldown.Initiative = 6f;
                    }
                    else
                    {
                        // units that join during regular round have to wait for one round
                        cooldown.Initiative = 0f;
                        cooldown.StandardAction = 6f;
                    }
                }
            }
        }

        public void HandleUnitJoinCombat(UnitEntityData unit)
        {
            Mod.Debug(MethodBase.GetCurrentMethod(), unit);

            if (Initialized)
            {
                AddUnit(unit);
            }
        }

        public void HandleUnitSpawned(UnitEntityData entityData)
        {
            Mod.Debug(MethodBase.GetCurrentMethod(), entityData);

            if (Initialized)
            {
                AddUnit(entityData);
            }
        }

        public void HandleUnitLeaveCombat(UnitEntityData unit)
        {
            Mod.Debug(MethodBase.GetCurrentMethod(), unit);

            if (Initialized)
            {
                 RemoveUnit(unit);
            }
        }

        public void HandleUnitDeath(UnitEntityData entityData)
        {
            Mod.Debug(MethodBase.GetCurrentMethod(), entityData);

            if (Initialized)
            {
                RemoveUnit(entityData);
            }
        }

        public void HandleUnitDestroyed(UnitEntityData entityData)
        {
            Mod.Debug(MethodBase.GetCurrentMethod(), entityData);

            if (Initialized)
            {
                RemoveUnit(entityData);
            }
        }

        // fix units will never leave combat if they become inactive (cause Call Forth Kanerah / Kalikke glitch)
        public void HandleObjectInGameChaged(EntityDataBase entityData)
        {
            if (entityData is UnitEntityData unit)
            {
                Mod.Debug(MethodBase.GetCurrentMethod(), unit);

                if (!unit.IsInGame && unit.IsInCombat)
                {
                    unit.LeaveCombat();
                }
            }
        }

        #endregion

        public class UnitsOrderComaprer : IComparer<UnitEntityData>
        {
            public int Compare(UnitEntityData x, UnitEntityData y)
            {
                if (x.IsCurrentUnit())
                    return -1;
                else if (y.IsCurrentUnit())
                    return 1;

                float xTime = x.GetTimeToNextTurn();
                float yTime = y.GetTimeToNextTurn();

                if (xTime.Approximately(yTime, 0.0001f))
                    return 0;
                else if (xTime < yTime)
                    return -1;
                else
                    return 1;
            }
        }
    }
}
