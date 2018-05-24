﻿//#define NO_RESPOND

using System;
using System.Collections.Generic;
using SystemData;
using Common;
using FrameWork;
using GameData;
using WorldServer.World.Objects.PublicQuests;
using WorldServer.World.Battlefronts.Keeps;
using WorldServer.Services.World;

namespace WorldServer
{
    public class AIInterface : BaseInterface
    {
        public static int BrainThinkTime = 3000; // Update think every 3sec
        public static int MaxAggroRange = 15; // Max Range To Aggro

        private Unit _unit;
        public Player Debugger { get; set; }

        private AiState _state = AiState.STANDING;
        public AiState State
        {
            get
            {
                return _state;
            }
            set
            {
                if (_state != value)
                {
                    if (value == AiState.FIGHTING)
                        OnCombatStart();
                    else if (value == AiState.STANDING && _state == AiState.FIGHTING)
                        OnCombatStop();
                    else if (value == AiState.MOVING)
                        OnWalkStart();
                    else if(_state == AiState.MOVING)
                        OnWalkEnd();

                    _state = value;
                }
            }
        }

        public override bool Load()
        {
            _unit = (Unit)_Owner;

            if (!(_Owner is Player))
            {
                if (CurrentBrain == null)
                    SetBrain(new DummyBrain(_unit));
            }

            _unit.EvtInterface.AddEventNotify(EventName.OnTargetDie, OnTargetDie);

            ResetThinkInterval();

            return base.Load();
        }

        private int _thinkInterval;
        private long _nextThinkTime;

        private void ResetThinkInterval()
        {
            if (_Owner is Pet || (_unit.Level == 55 && _unit is Creature))
            {
                _thinkInterval = 1000;
            }

            else
                _thinkInterval = BrainThinkTime;
        }

        public override void Update(long tick)
        {
            #if USE_WAYPOINTS
            if (_Owner.IsUnit())
            {
                if (!_unit.IsDead)
                    UpdateWaypoints(tick);
            }
            #endif

            if (tick > _nextThinkTime)
            {
                UpdateThink();

                _nextThinkTime = tick + _thinkInterval;
            }

            base.Update(tick);
        }

        public bool OnTargetDie(Object obj, object args)
        {
            if (State == AiState.FIGHTING)
            {
                LockMovement(4000);
            }

            CurrentBrain.NotifyTargetKilled((Unit)obj);
            return false;
        }

        #region States

        public Item EquipedItem;

        public void OnCombatStart()
        {
            /*
            if (EquipedItem == null && _Owner is Creature)
            {
                EquipedItem = _Owner.GetCreature().ItmInterface.RemoveCreatureItem(11);
            }
            */
        }

        public void OnCombatStop()
        {
            /*
            if (EquipedItem != null && _Owner is Creature)
            {
                _Owner.GetCreature().ItmInterface.AddCreatureItem(EquipedItem);
                EquipedItem = null;
            }
            */
        }

        public void OnWalkStart()
        {

        }

        public void OnWalkEnd()
        {

        }

        #endregion

        #region Brain

        public ABrain CurrentBrain;

        protected Dictionary<ushort, AggroInfo> Aggros;

        public void SetBrain(ABrain newBrain)
        {
            CurrentBrain?.Stop();

            CurrentBrain = newBrain;

            if (Aggros == null)
                Aggros = new Dictionary<ushort, AggroInfo>();

            CurrentBrain?.Start(Aggros);
        }

        public void UpdateThink()
        {
            if (HasPlayer())
                return;

            if (_unit != null)
            {
                if (_unit is KeepNpcCreature.KeepCreature)
                {
                    KeepNpcCreature.KeepCreature npc = _unit as KeepNpcCreature.KeepCreature;
                    if (((npc.Z - npc.SpawnPoint.Z > 120 || npc.SpawnPoint.Z - npc.Z > 30)) || !npc.PointWithinRadiusFeet(npc.WorldSpawnPoint, 200))
                    {
                        ProcessCombatEnd();
                        return;
                    }
                }
            }

            if (CurrentBrain != null && CurrentBrain.IsStart && !CurrentBrain.IsStop)
            {
                CurrentBrain.Think();
                if (State == AiState.FIGHTING)
                    CurrentBrain.Fight();
            }

            Creature creature = _unit as Creature;

            if (creature == null || creature is Pet)
                return;

            if (!creature.PointWithinRadiusFeet(creature.WorldSpawnPoint, 200))
                ProcessCombatEnd();

        }

 #region Combat Events

        /// <summary> Attempts to initiate AI-controlled combat. </summary>
        public void ProcessCombatStart(Unit target)
        {
            #if DEBUG && NO_RESPOND
            if (!(_unit is Pet))
                return;
            #endif

            if (_unit == null)
                return;

            if (target == null)
            {
                Log.Error("AIInterface for " + _unit, "ProcessCombatStart with NULL target");
                return;
            }

            if (!CurrentBrain.StartCombat(target))
                return;

            if (!(_unit is KeepNpcCreature.KeepCreature))
                _thinkInterval = 650;

            bool processLink = State != AiState.FIGHTING && target is Player && _unit.Aggressive;

            State = AiState.FIGHTING;
            _unit.CbtInterface.RefreshCombatTimer();

            // Social for PQuestCreatures
            if (processLink && _Owner is PQuestCreature)
            {
                if (IsInDungeon())
                {
                    foreach (Object obj in _Owner.ObjectsInRange)
                    {
                        PQuestCreature crea = obj as PQuestCreature;
                        if (crea != null && _Owner.ObjectWithinRadiusFeet(crea, 40) && crea.Aggressive)
                        {
                            ((Creature)obj).AiInterface.ProcessLink(target);
                        }
                    }
                }
                else
                {
                    uint playerCount = 0;
                    foreach (Object obj in _Owner.ObjectsInRange)
                    {
                        if (obj is Player)
                        {
                            playerCount++;
                        }
                    }

                    uint counter = 0;
                    foreach (Object obj in _Owner.ObjectsInRange)
                    {
                        PQuestCreature crea = obj as PQuestCreature;
                        if (crea != null && _Owner.ObjectWithinRadiusFeet(crea, 30) && crea.Aggressive)
                        {
                            counter++;
                            if (playerCount < 6 && playerCount < counter)
                                break;

                            ((Creature)obj).AiInterface.ProcessLink(target);
                        }
                    }
                }
            }
        }

        public bool IsInDungeon()
        {
            bool dungeon = false;

            switch (_Owner.ZoneId)
            {
                case 60:
                    dungeon = true;
                    break;
            }

            return dungeon;
        }

        public void ProcessLink(Unit target)
        {
            if (State == AiState.FIGHTING || !CurrentBrain.StartCombat(target))
                return;

            State = AiState.FIGHTING;
            _unit.CbtInterface.RefreshCombatTimer();
        }

        /// <summary>
        /// <para>Attempts to have this unit leave AI-controlled combat.</para>
        /// <para>Called when no targets remain alive or in range to be attacked, when the NPC leashes or is ordered to disengage, and when the NPC dies.</para>
        /// </summary>
        public void ProcessCombatEnd()
        {
            if (State != AiState.FIGHTING)
                return;
            if (CurrentBrain.EndCombat())
            {
                ResetThinkInterval();
                State = AiState.STANDING;
            }
        }

        public void ProcessAttacked(Unit attacker)
        {
            switch (State)
            {
                case AiState.STANDING:
                case AiState.MOVING:
                    ProcessCombatStart(attacker);
                    break;
                default:
                    return;
            }

            CurrentBrain?.AddHatred(attacker, attacker is Player, 1);
        }

        public void ProcessTakeDamage(Unit fighter, uint damage, float hatredMod, uint mitigation = 0)
        {
            switch (State)
            {
                case AiState.STANDING:
                case AiState.MOVING:
                    ProcessCombatStart(fighter);
                    break;
            }

            uint hateCaused = (uint)((damage + mitigation) * hatredMod * fighter.StsInterface.GetStatPercentageModifier(Stats.HateCaused));

            fighter.BuffInterface.CheckGuardHate(CurrentBrain, ref hateCaused);

            CurrentBrain?.AddHatred(fighter, fighter.IsPlayer(), hateCaused);
        }

        public void ProcessInflictDamage(Unit victim)
        {
            switch (State)
            {
                case AiState.STANDING:
                case AiState.MOVING:
                    ProcessCombatStart(victim);
                    break;
            }
        }

        public void ProcessTaunt(Unit taunter, byte lvl)
        {
            if (_unit is Pet)
                return;

            _unit.CbtInterface.RefreshCombatTimer();
            taunter.CbtInterface.RefreshCombatTimer();

            switch (State)
            {
                case AiState.STANDING:
                case AiState.MOVING:
                    ProcessCombatStart(taunter);
                    break;
            }

            CurrentBrain.OnTaunt(taunter, lvl);
        }

        public void OnOwnerDied()
        {
            if (_unit is Player)
                return;

            ProcessCombatEnd();
        }

        #endregion

        #endregion

        #region Ranged

        public List<Unit> RangedAllies = new List<Unit>();
        public List<Unit> RangedEnemies = new List<Unit>();

        public bool AddRange(Unit unit)
        {
            if (!HasUnit())
                return false;

            if (CombatInterface.IsEnemy(GetUnit(), unit))
            {
                if ((unit.Realm == Realms.REALMS_REALM_NEUTRAL && !unit.Aggressive) || (!unit.IsPlayer() && !unit.Aggressive) || (!GetUnit().IsPlayer() && !GetUnit().Aggressive) || (unit is Creature && ((Creature) unit).Entry == 47) /*|| (unit is Creature && !unit.IsPlayer() && !unit.IsGameObject() && IsNeutralFaction(unit as Creature))*/)
                    return true;

                lock (RangedEnemies)
                    RangedEnemies.Add(unit.GetUnit());
            }
            else
            {
                lock(RangedAllies)
                    RangedAllies.Add(unit.GetUnit());
            }

            return true;
        }

        public bool RemoveRange(Unit unit)
        {
            if (!HasUnit())
                return false;

            RangedAllies.Remove(unit);
            RangedEnemies.Remove(unit);
            return true;
        }

        public void ClearRange()
        {
            RangedAllies.Clear();
            RangedEnemies.Clear();
        }

        /*private bool IsNeutralFaction(Creature c)
        {
            if (c.Spawn.Faction == 0 || (((c.Spawn.Faction >> 8 & 0x1))) == 0) return true;
            else return false;
        }*/

        public Unit GetAttackableUnit()
        {
            Unit unitOwner = GetUnit();

            float maxRange = MaxAggroRange + unitOwner.Level / 1.5f;
            Unit target = null;

            lock (RangedEnemies)
                foreach (Unit enemy in RangedEnemies)
                {
                    Player player = enemy as Player;

                    if (player != null && player.StealthLevel > 0 && unitOwner.Level <= player.EffectiveLevel + 2)
                        continue;

                    if (!CombatInterface.CanAttack(unitOwner, enemy))
                        continue;

                    float dist = _Owner.GetDistanceToObject(enemy, true);

                    if (dist > maxRange)
                        continue;

                    if (unitOwner != null)
                    {
                        if (unitOwner is KeepNpcCreature.KeepCreature)
                        {
                            KeepNpcCreature.KeepCreature npc = unitOwner as KeepNpcCreature.KeepCreature;
                            //Keep NPCs have additional hardening. If their target goes outside the keep, instantly reset.
                            if (enemy.Z - npc.SpawnPoint.Z > 120 || npc.SpawnPoint.Z - enemy.Z > 30 || !enemy.PointWithinRadiusFeet(npc.WorldSpawnPoint, 200) || !npc.PointWithinRadiusFeet(npc.WorldSpawnPoint, 200))
                            {
                                continue;
                            }
                            if (!enemy.LOSHit(npc))
                            {
                                continue;
                            }
                        }
                        else if (unitOwner is Creature)
                        {
                            Creature creature = unitOwner as Creature;
                            if (creature is Pet)
                            {
                                Pet pet = creature as Pet;
                               
                                if (pet == null || pet.Owner == null)
                                    continue;
                                //Only aggro if we have LOS to the owner.
                                if (!enemy.LOSHit(pet.Owner))
                                {
                                    continue;
                                }

                            }
                            else
                            {
                                //Standard NPC checks.
                                if (!creature.PointWithinRadiusFeet(creature.WorldSpawnPoint, 200))
                                {
                                    continue;
                                }
                                if (!enemy.LOSHit(creature))
                                {
                                    continue;
                                }
                            }
                        }
                        else
                        {

                        }
                    }

                    maxRange = dist;
                    target = enemy;
                }

            return target;
        }

        #endregion

        #region Waypoints

        public List<Waypoint> Waypoints = new List<Waypoint>();
        public Waypoint CurrentWaypoint;
        public byte CurrentWaypointType = Waypoint.Loop;
        public bool IsWalkingBack; // False : Running on waypooints Start to End
        public int CurrentWaypointID = -1;
        public long NextAllowedMovementTime;
        public bool Started;
        public bool Ended;

        // Waypoints
        private static readonly object WaypointsTableLock = new object();

        public void AddWaypoint(Waypoint AddWp)
        {
            //System.Diagnostics.Trace.Assert(_Owner.Name != "Heinz Lutzen");
            if (_Owner.IsCreature())
            {
                if (Waypoints.Count == 0)
                {
                    Waypoint StartWp = new Waypoint
                    {
                        CreatureSpawnGUID = _Owner.GetCreature().Spawn.Guid,
                        GameObjectSpawnGUID = _Owner.Oid,
                        X = (ushort) _Owner.X,
                        Y = (ushort) _Owner.Y,
                        Z = (ushort) _Owner.Z,
                        Speed = AddWp.Speed,
                        WaitAtEndMS = AddWp.WaitAtEndMS
                    };

                    lock (WaypointsTableLock)
                    {
                        StartWp.GUID = Convert.ToUInt32(WorldMgr.Database.GetNextAutoIncrement<Waypoint>());
                        Waypoints.Add(StartWp);
                        WaypointService.DatabaseAddWaypoint(StartWp);
                    }
                    // lock (WaypointsTableLock)
                }
                AddWp.CreatureSpawnGUID = _Owner.GetCreature().Spawn.Guid;
                AddWp.GameObjectSpawnGUID = _Owner.Oid;
                lock (WaypointsTableLock)
                {
                    AddWp.GUID = Convert.ToUInt32(WorldMgr.Database.GetNextAutoIncrement<Waypoint>());
                    Waypoints.Add(AddWp);
                    WaypointService.DatabaseAddWaypoint(AddWp);
                }
                // lock (WaypointsTableLock)
                Waypoint PrevWp = Waypoints[Waypoints.Count - 1];
                PrevWp.NextWaypointGUID = AddWp.GUID;
                SaveWaypoint(PrevWp);
            }
        }

        public void SaveWaypoint(Waypoint SaveWp)
        {
            WaypointService.DatabaseSaveWaypoint(SaveWp);
        }

        public void RemoveWaypoint(Waypoint RemoveWp)
        {
            switch (Waypoints.Count)
            {
                case 0:
                case 1:
                    break;
                case 2:
                    lock (WaypointsTableLock)
                    {
                        foreach (Waypoint Wp in Waypoints)
                        {
                            WaypointService.DatabaseDeleteWaypoint(Wp);
                        }
                        Waypoints.Clear();
                    } //lock
                    break;
                default:
                    lock (WaypointsTableLock)
                    {
                        int Index = -1;
                        foreach (Waypoint Wp in Waypoints)
                        {
                            if (Wp.GUID == RemoveWp.GUID)
                            {
                                Index = Waypoints.IndexOf(Wp);
                            }
                        }
                        if (Index != -1)
                        {
                            if (Index != 0)
                            {
                                if (Index == Waypoints.Count)
                                {
                                    Waypoints[Index - 1].NextWaypointGUID = 0;
                                }
                                else
                                {
                                    Waypoints[Index - 1].NextWaypointGUID = Waypoints[Index].NextWaypointGUID;
                                }

                                WaypointService.DatabaseSaveWaypoint(Waypoints[Index - 1]);
                                WaypointService.DatabaseDeleteWaypoint(Waypoints[Index]);
                                Waypoints.RemoveAt(Index);
                            }
                        }
                    }
                    // lock (WaypointsTableLock)
                    break;
            } // switch
        }

        public void RemoveWaypoint(int WaypointGUID)
        {
            RemoveWaypoint(GetWaypoint(WaypointGUID));
        }

        public void RandomizeWaypoint(Waypoint RandomWp)
        {
            if (_Owner.GetCreature() != null)
            {
                RandomWp.X = (ushort)(_Owner.GetCreature().X + StaticRandom.Instance.Next(50) + StaticRandom.Instance.Next(100) + StaticRandom.Instance.Next(150));
                RandomWp.Y = (ushort)(_Owner.GetCreature().Y + StaticRandom.Instance.Next(50) + StaticRandom.Instance.Next(100) + StaticRandom.Instance.Next(150));
                RandomWp.Z = (ushort)_Owner.GetCreature().Z;
                RandomWp.Speed = 10;
                RandomWp.WaitAtEndMS = (uint)(5000 + StaticRandom.Instance.Next(10) * 1000);
                SaveWaypoint(RandomWp);
            }
        }

        public Waypoint GetWaypoint(int WaypointGUID)
        {
            foreach (Waypoint Wp in Waypoints)
            {
                if (Wp.GUID == WaypointGUID)
                {
                    return Wp;
                }
            }
            return null;
        }

        public void UpdateWaypoints(long Tick)
        {
            if (State == AiState.STANDING || State == AiState.MOVING)
            {
                if (Waypoints.Count == 0)
                    return;

                //System.Diagnostics.Trace.Assert(_Owner.Name != "Heinz Lutzen");

                if (CurrentWaypoint != null && IsAtWaypointEnd())
                    EndWaypoint(Tick);

                if (CanStartNextWaypoint(Tick))
                    SetNextWaypoint(Tick);

                if (State == AiState.MOVING && !_unit.MvtInterface.IsMoving)
                    State = AiState.STANDING;

                if (State == AiState.STANDING && CurrentWaypoint != null)
                {
                    StartWaypoint(Tick);
                }
            }
        }

        public bool IsAtWaypointEnd()
        {
            if (_Owner.GetDistanceTo(CurrentWaypoint.X, CurrentWaypoint.Y, CurrentWaypoint.Z) < 3)
                return true;

            return true;
        }

        public bool CanStartNextWaypoint(long Tick)
        {
            if (CurrentWaypoint != null)
                return false;

            if (Tick <= NextAllowedMovementTime)
                return false;

            return true;
        }

        public void SetNextWaypoint(long Tick)
        {
            if (!IsWalkingBack)
                ++CurrentWaypointID;
            else
                --CurrentWaypointID;

            if (CurrentWaypointID < 0)
            {
                if (CurrentWaypointType == Waypoint.Loop)
                {
                    IsWalkingBack = false;
                    CurrentWaypointID = 0;
                }
            }
            else if (CurrentWaypointID >= Waypoints.Count)
            {
                if (CurrentWaypointType == Waypoint.Loop)
                {
                    IsWalkingBack = true;
                    CurrentWaypointID = Waypoints.Count - 2;
                }
            }

            if (CurrentWaypointID >= Waypoints.Count || CurrentWaypointID < 0)
            {
                CurrentWaypoint = null;
            }
            else
            {
                CurrentWaypoint = Waypoints[CurrentWaypointID];
                StartWaypoint(Tick);
            }
        }

        public void StartWaypoint(long Tick)
        {
            //Log.Info("Waypoints", "Starting Waypoint");

            State = AiState.MOVING;
            _unit.MvtInterface.Move(new Point3D(CurrentWaypoint.X, CurrentWaypoint.Y, CurrentWaypoint.Z));
            if (!Started)
            {
                // TODO : Messages,Emotes, etc

                if(CurrentWaypoint.TextOnStart != "")
                    _unit.Say(CurrentWaypoint.TextOnStart, ChatLogFilters.CHATLOGFILTERS_MONSTER_SAY);
            }

            Started = true;
            Ended = false;
        }

        public void EndWaypoint(long Tick)
        {
            //Log.Info("Waypoints", "Ending Waypoint");

            if (!Ended)
            {
                // TODO : Messages, Emote, etc
                if(CurrentWaypoint.TextOnEnd != "")
                    _unit.Say(CurrentWaypoint.TextOnEnd, ChatLogFilters.CHATLOGFILTERS_MONSTER_SAY);
            }

            NextAllowedMovementTime = Tick + CurrentWaypoint.WaitAtEndMS;
            CurrentWaypoint = null;
            Started = false;
            Ended = true;
        }

        public void LockMovement(long MSTime)
        {
            if (MSTime == 0)
                NextAllowedMovementTime = 0;
            else
                NextAllowedMovementTime = TCPManager.GetTimeStampMS() + MSTime;
        }

        #endregion
    }
}
