﻿using System;
using System.Collections.Generic;
using System.Linq;
using SystemData;
using FrameWork;
using GameData;

namespace WorldServer
{
    public abstract class ABrain
    {
        public bool IsStart;
        public bool IsStop;

        protected Unit _unit;
        protected Pet _pet;
        protected CombatInterface_Npc Combat;
        protected AIInterface AI;

        protected ABrain(Unit unit)
        {
            _unit = unit;
            Combat = (CombatInterface_Npc)unit.CbtInterface;
            AI = unit.AiInterface;
            _pet = unit as Pet;
        }

        public virtual bool Start(Dictionary<ushort, AggroInfo> aggros)
        {
            if (IsStart)
                return false;

            Aggros = aggros;
            IsStart = true;
            return true;
        }

        public virtual bool Stop()
        {
            if (IsStop)
                return false;

            IsStop = true;
            return true;
        }

        public virtual void Think()
        {

        }

        private long _nextDistanceCheckTime;

        #region Combat
        /// <summary> Causes the NPC to begin attacking the specified unit. </summary>
        /// <notes>Only needs to be overridden for pets.</notes>

        private long _combatStart;

        public virtual bool StartCombat(Unit fighter)
        {
            if (_unit.IsDead)
                return false;

            // We try to buff NPC here
            BuffAtCombatStart();

            _combatStart = TCPManager.GetTimeStampMS();

            GetAggro(fighter.Oid).DamageReceived += 100;

            AI.Debugger?.SendClientMessage("[MR]: Started combat with "+fighter.Name+".");

            Combat.SetTarget(fighter, TargetTypes.TARGETTYPES_TARGET_ENEMY);

            //if (fighter != null && _unit.IsKeepLord)
                //_unit.Say("Die " + fighter.Name + "!");

            Chase(fighter);

            return true;
        }

        public void Chase(Unit fighter, bool ForceMove = false, bool RangeOverride = false, int NewRange = 0)
        {
            if (fighter == null)
                return;

            Creature crea = _unit as Creature;

            if (!RangeOverride)
                NewRange = crea.Ranged;

            if (crea == null)
                _unit.MvtInterface?.Follow(fighter, 5, 6, false, ForceMove);
            else if (NewRange == 0)
                _unit.MvtInterface?.Follow(fighter, (int)crea.BaseRadius, (int)crea.BaseRadius + 1, false, ForceMove);
            else
                _unit.MvtInterface.Follow(fighter, Math.Max(5, NewRange - 5), Math.Max(6, NewRange), false, ForceMove);
        }

        public virtual void Fight()
        {
            // Here we are processng aggro every 0.25 s
            if (WorldMgr.WorldSettingsMgr.GetGenericSetting(11) == 1)
                ProcessAggro();
            // Check for pet leashing.
            if (_pet != null)
            {
                long tick = TCPManager.GetTimeStampMS();

                if (tick > _nextDistanceCheckTime)
                {
                    _nextDistanceCheckTime = tick + 250;

                    if (_pet.Owner != null && !_pet.WorldPosition.IsWithinRadiusFeet(_pet.Owner.WorldPosition, 250))
                    {
                        _pet.Owner.SendClientMessage("Your pet is too far away from you, and has been dismissed.", ChatLogFilters.CHATLOGFILTERS_ABILITY_ERROR);
                        _pet.Dismiss(null, null);
                        return;
                    }

                    Player playerTarget = Combat.CurrentTarget as Player;
                    if (playerTarget != null && (playerTarget.StealthLevel > 0) && !_pet.WorldPosition.IsWithinRadiusFeet(playerTarget.WorldPosition, 30))
                    {
                        _pet.AiInterface.ProcessCombatEnd();
                        AI.Debugger?.SendClientMessage("[MR]: Lost track of stealthed player " + playerTarget.Name + ", ending combat.");
                        return;
                    }
                }
            }

            if (Combat != null && Combat.CurrentTarget != null && (Combat.CurrentTarget.IsDead || Combat.CurrentTarget.PendingDisposal || Combat.CurrentTarget.IsDisposed))
            {
                AI.Debugger?.SendClientMessage("[MR]: Current target is dead or disposed, reacquiring...");
                Aggros.Remove(Combat.CurrentTarget.Oid);

                Combat.SetTarget(null, TargetTypes.TARGETTYPES_TARGET_ENEMY);

                if (_pet != null)
                {
                    if (_pet.AIMode != (byte)PetCommand.Passive && !_pet.IsHeeling)
                    {
                        AI.Debugger?.SendClientMessage("[MR]: Pet seeking attackable unit...");
                        Combat.SetTarget(_pet.AiInterface.GetAttackableUnit(), TargetTypes.TARGETTYPES_TARGET_ENEMY);
                    }
                }

                else
                {
                    Unit nextTarget = GetNextTarget();
                    Combat.SetTarget(nextTarget, TargetTypes.TARGETTYPES_TARGET_ENEMY);
                    if (nextTarget != null && _unit.IsKeepLord)
                        _unit.Say("Die " + nextTarget.Name + "!");
                }

                    if (Combat.CurrentTarget != null)
                    StartCombat(Combat.CurrentTarget);

                else
                {
                    AI.Debugger?.SendClientMessage("[MR]: Failed to acquire a new target. Disengaging.");
                    AI.ProcessCombatEnd();
                    return;
                }
            }

            if (_unit != null && !_unit.IsDisabled || _unit != null && !_unit.IsStaggered)
            {
                Combat.TryAttack();
                TryUseAbilities();
            }

            foreach (Player player in _unit.PlayersInRange.ToList())
            {
                if (player != null && player.DebugMode)
                    player.SendClientMessage("[MR]: Unit: " + _unit.Name + " (OID: " + _unit.Oid + ") Current Hate: " + GetAggro(player.Oid).Hatred);
            }
        }
        /// <summary>
        /// This method assigns aggro from heals and selects new target, based on current max hate
        /// </summary>
        private void ProcessAggro()
        {
            if (!_unit.IsPet())
            {
                ulong maxHatred = 0;
                int nextTargetOid = 0;
                Unit nextTarget = null;

                foreach (Player player in _unit.PlayersInRange.ToList())
                {
                    if (player != null && CombatInterface.CanAttack(_unit, player))
                    {
                        if (player.IsDead)
                        {
                            Aggros[player.Oid].Hatred = 0;
                        }
                        else
                        {
                            if (player.CbtInterface.IsInCombat)
                            {
                                foreach (KeyValuePair<ushort, AggroInfo> healAggro in player.HealAggros)
                                {
                                    foreach (KeyValuePair<ushort, AggroInfo> aggro in Aggros)
                                    {
                                        if (!(_unit is Pet) && healAggro.Key == aggro.Key && _combatStart < healAggro.Value.HealingReceivedTime && aggro.Value.HealingReceivedTime != healAggro.Value.HealingReceivedTime)
                                        {
                                            aggro.Value.Hatred += (ulong)((healAggro.Value.HealingReceived) * GetDetaunt(healAggro.Key));
                                            healAggro.Value.HealingReceivedTime = aggro.Value.HealingReceivedTime;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                foreach (Object obj in _unit.ObjectsInRange.ToList())
                {
                    Unit u = obj as Unit;
                    if (u != null && (!u.IsDead && !u.PendingDisposal && !u.IsDisposed) && CombatInterface.CanAttack(_unit, u))
                    {
                        foreach (KeyValuePair<ushort, AggroInfo> aggro in Aggros)
                        {
                            if (aggro.Key == obj.Oid)
                            {
                                if (aggro.Value.Hatred > maxHatred)
                                {
                                    maxHatred = aggro.Value.Hatred;
                                    nextTargetOid = obj.Oid;
                                    nextTarget = u;
                                }
                            }
                        }
                    }
                }

                if (nextTarget != null && _pet == null)
                    AddHatred(nextTarget, nextTarget.IsPlayer(), 0);
            }
        }

        /// <summary> Causes disengagement with the current target and a return to the spawnpoint.</summary>
        /// <notes>Only needs to be overridden for pets.
        /// Call only from AiInterface</notes>
        public virtual bool EndCombat()
        {
            AI.Debugger?.SendClientMessage("[MR]: Ending combat.");
            Combat.SetTarget(null, TargetTypes.TARGETTYPES_TARGET_ENEMY);

            _unit.MvtInterface.StopMove();

            Aggros = new Dictionary<ushort, AggroInfo>();

            if (_pet == null)
            {
                if (_unit is Creature && !_unit.IsDead)
                    ReturnToSpawn();

                Combat.FirstStriker = null;
            }

            // Heel the pet
            else
            {
                AI.Debugger?.SendClientMessage("[MR]: Pet heeling as combat is over.");
                _pet.Recall();
            }

            _unit.EvtInterface.Notify(EventName.OnLeaveCombat, _unit, null);
            if (_unit is Creature && _unit.AbtInterface.NPCAbilities != null)
            { 
                foreach (NPCAbility ability in _unit.AbtInterface.NPCAbilities)
                    ability.AbilityUsed = 0;
            }

            return true;
        }

        private void ReturnToSpawn()
        {
            AI.Debugger?.SendClientMessage("[MR]: Retreating to the spawnpoint.");
            if (_unit.IsInstanceSpawn())
            {
                InstanceSpawn npc = (InstanceSpawn)_unit;
                npc.BuffInterface.RemoveAllBuffs();
                npc.ReceiveHeal(null, npc.MaxHealth);
                npc.SetPosition((ushort)npc.SpawnPoint.X, (ushort)npc.SpawnPoint.Y, (ushort)npc.SpawnPoint.Z, npc.SpawnHeading, npc.Spawn.ZoneId, true);
            }
            else
            {
                Creature npc = (Creature)_unit;
                npc.BuffInterface.RemoveAllBuffs();
                npc.ReceiveHeal(null, npc.MaxHealth);
                npc.SetPosition((ushort)npc.SpawnPoint.X, (ushort)npc.SpawnPoint.Y, (ushort)npc.SpawnPoint.Z, npc.SpawnHeading, npc.Spawn.ZoneId, true);
            }
        }
        #endregion

        #region Ability Usage

        private long _nextTryCastTime;
        private long _oneshotPercentCast = 0;

        protected void BuffAtCombatStart()
        {
            if (_unit.AbtInterface.NPCAbilities == null)
                return;

            foreach (NPCAbility ability in _unit.AbtInterface.NPCAbilities)
            {
                // If ability is set to Active = 0 it will not be played
                if (ability.Active == 0 || ability.ActivateOnCombatStart == 0) continue;

                //_unit.AbtInterface.StartCast(_unit, ability.Entry, 1);
                BuffInfo b = AbilityMgr.GetBuffInfo(ability.Entry, _unit, _unit); // This should cast buff on self
                _unit.BuffInterface.QueueBuff(new BuffQueueInfo(_unit, _unit.Level, b));
            }
        }

        protected void SetOldTarget(object creature)
        {
            Combat.SetTarget(GetNextTarget(), TargetTypes.TARGETTYPES_TARGET_ENEMY);
        }

        protected void TryUseAbilities()
        {
            if (_unit.AbtInterface.NPCAbilities == null)
                return;

            if (Combat.IsFighting && Combat.CurrentTarget != null && _unit.AbtInterface.CanCastCooldown(0) && TCPManager.GetTimeStampMS() > _nextTryCastTime)
            {
                long curTimeMs = TCPManager.GetTimeStampMS();

                float rangeFactor = _unit.StsInterface.GetStatPercentageModifier(Stats.Range);

                uint AllowPercentAbilityCycle = 0;

                if (CurTarget != null)
                {
                    var prm = new List<object>() { CurTarget };
                    _unit.EvtInterface.AddEvent(SetOldTarget, 2000, 1, prm);
                    CurTarget = null;
                }

                foreach (NPCAbility ability in _unit.AbtInterface.NPCAbilities)
                {
                    // If ability is set to Active = 0 it will not be played
                    if (ability.Active == 0) continue;

                    // If target is below MinRange that is set in creature_abilities we don't want to run this particular skill
                    if (ability.MinRange != 0 && _unit.GetDistanceToObject(_unit.CbtInterface.GetCurrentTarget()) < ability.MinRange)
                        continue;

                    // This disable Shockwave (punt) for Keep Lords that are inside room
                    if (ability.Entry == 13528 && _unit.GetDistanceToWorldPoint(_unit.WorldSpawnPoint) < 49)
                    {
                        continue;
                    }

                    if (ability.DisableAtHealthPercent != 0)
                    {
                        if (_unit.Health+1 < (_unit.TotalHealth * ability.DisableAtHealthPercent) / 100)
                            continue;
                    }

                    if (ability.ActivateAtHealthPercent != 0)
                    {
                        // This checks if we can add new ability to ability cycle
                        if (ability.AbilityCycle == 1 && _unit.Health < (_unit.TotalHealth * ability.ActivateAtHealthPercent) / 100 )
                            AllowPercentAbilityCycle = 1;

                        // This checks if we can reset the ability if NPC healed - if it's still on cooldwon, we do not refresh it
                        if (ability.AbilityCycle == 0 && ability.AbilityUsed == 1 && (_unit.Health > (_unit.TotalHealth * ability.ActivateAtHealthPercent) / 100) && _oneshotPercentCast < curTimeMs)
                            ability.AbilityUsed = 0;
                        
                        // This will play ability after NPC is wounded below X %
                        if (ability.AbilityCycle == 0 && ability.AbilityUsed == 0 && (_unit.Health < (_unit.TotalHealth * ability.ActivateAtHealthPercent) / 100) && _oneshotPercentCast < curTimeMs)
                        {
                            // This set random target if needed
                            if (ability.RandomTarget == 1)
                                SetRandomTarget();

                            // This list of parameters is passed to the function that delays the cast by 1000 ms
                            var prms = new List<object>() { _unit, ability.Entry, ability.RandomTarget };

                            if (ability.Text != "") _unit.Say(ability.Text.Replace("<character name>", _unit.CbtInterface.GetCurrentTarget().Name));
                            _unit.EvtInterface.AddEvent(StartDelayedCast, 1000, 1, prms);
                            _oneshotPercentCast = TCPManager.GetTimeStampMS() + ability.Cooldown * 1000;
                            ability.AbilityUsed = 1;
                            continue;
                        }
                    }

                    if (ability.AutoUse && !_unit.AbtInterface.IsCasting() && ability.CooldownEnd < curTimeMs && _unit.AbtInterface.CanCastCooldown(ability.Entry) && curTimeMs > _combatStart + ability.TimeStart * 1000) 
                    {
                        uint ExtraRange = 0;
                        if (Combat != null && Combat.CurrentTarget != null && Combat.CurrentTarget.IsMoving)
                            ExtraRange = 5;


                        if ((ability.Range == 0 || _unit.IsInCastRange(Combat.CurrentTarget, Math.Max(5 + ExtraRange, (uint)(ability.Range * rangeFactor)))))
                        {
                            if (ability.ActivateAtHealthPercent == 0 || AllowPercentAbilityCycle == 1)
                            {
                                if (!_unit.LOSHit(Combat.CurrentTarget))
                                    _nextTryCastTime = TCPManager.GetTimeStampMS() + 1000;
                                else
                                {
                                    // This set random target if needed
                                    if (ability.RandomTarget == 1)
                                        SetRandomTarget();

                                    // This list of parameters is passed to the function that delays the cast by 1000 ms
                                    var prms = new List<object>() { _unit, ability.Entry, ability.RandomTarget };

                                    if (ability.Text != "") _unit.Say(ability.Text.Replace("<character name>", _unit.CbtInterface.GetCurrentTarget().Name), ChatLogFilters.CHATLOGFILTERS_MONSTER_SAY);
                                    _unit.EvtInterface.AddEvent(StartDelayedCast, 1000, 1, prms);

                                    //_unit.AbtInterface.StartCast(_unit, ability.Entry, 1);
                                    ability.CooldownEnd = curTimeMs + ability.Cooldown * 1000;
                                }

                                break;
                            }
                        }
                        else
                        {
                            //AbilityInfo abInfo = AbilityMgr.GetAbilityInfo(abilityID);
                            Chase(_unit.CbtInterface.GetCurrentTarget(), true, true, ability.Range);
                        }
                    }

                }
            }
        }

        private void SetRandomTarget()
        {
            AggroInfo maxAggro = null;
            Player plr = null;
            var delayedAggroRemoval = new List<object>();

            List<Player> plrsInRange = new List<Player>();
            foreach (Player player in _unit.PlayersInRange)
            {
                if (player != null && !player.IsDead && !player.IsInvulnerable && player.StealthLevel != 2)
                {
                    plrsInRange.Add(player);
                }
            }

            int rndmPlr = 0;
            if (plrsInRange.Count() > 0)
                rndmPlr = random.Next(1, plrsInRange.Count() + 1);

            if (rndmPlr != 0)
            {
                CurTarget = Combat.CurrentTarget;
                plr = plrsInRange.ElementAt(rndmPlr - 1);
                if (plr.Oid != CurTarget.Oid)
                {
                    maxAggro = GetMaxAggroHate();
                    AddHatred(plr, true, (long)maxAggro.Hatred - (long)GetAggro(plr.Oid).Hatred + 5000);
                    _unit.Say("Switching target to " + plr.Name);
                    delayedAggroRemoval.Add(plr);
                }
            }

            // Delay
            if (maxAggro != null && plr != null && !plr.IsDead)
            {
                plr.EvtInterface.AddEvent(DelayedAggroRemoval, 1000, 1, delayedAggroRemoval);
            }
        }

        private void DelayedAggroRemoval(object creature)
        {
            var Params = (List<object>)creature;

            Unit _target = Params[0] as Unit;

            if (_target != null)
            {
                AddHatred(_target, true, -5000);
            }

            
        }

        Random random = new Random();
        Unit CurTarget;

        private void StartDelayedCast(object creature)
        {
            var Params = (List<object>)creature;

            Unit _unit = Params[0] as Unit;
            ushort Ability = (ushort)Params[1];

            // This is used by random ability cast
            if (_unit != null && !_unit.IsDead)
            {
                _unit.AbtInterface.StartCast(_unit, Ability, 1);

                if (!(_unit is Pet))
                {
                    _unit.MvtInterface.StopMove();
                    AbilityInfo AbiInfo = AbilityMgr.GetAbilityInfo(Ability);
                    if (!AbiInfo.CanCastWhileMoving)
                        _unit.EvtInterface.AddEvent(DelayedChase, AbiInfo.CastTime + 100, 1);
                    else
                        Chase(_unit.CbtInterface.GetCurrentTarget(), true);
                }
            }
        }

        public void DelayedChase()
        {
            Chase(_unit.CbtInterface.GetCurrentTarget(), true);
        }

        #endregion

        #region AggroSystem

        protected Dictionary<ushort, AggroInfo> Aggros;

        protected AggroInfo GetAggro(ushort oid)
        {
            AggroInfo info;

            if (Aggros.TryGetValue(oid, out info))
                return info;

            info = new AggroInfo(oid);
            Aggros.Add(oid, info);

            return info;
        }

        public AggroInfo GetMaxAggroHate()
        {
            AggroInfo maxAggro = null;
            float maxHate = -1;

            foreach (AggroInfo aggro in Aggros.Values)
            {
                float hate = aggro.GetHate();

                if (hate > maxHate)
                {
                    maxHate = hate;
                    maxAggro = aggro;
                }
            }

            return maxAggro;
        }

        public virtual void AddHatred(Unit fighter, bool isPlayer, long hatred)
        {
            AggroInfo info = GetAggro(fighter.Oid);

            long temporaryHatred = (long)info.Hatred;

            temporaryHatred += hatred;

            if (temporaryHatred < 0)
                temporaryHatred = 0;

            info.Hatred = (ulong)temporaryHatred;

            if (_unit is Pet)
                return;

            if (!fighter.IsDead && Combat.CurrentTarget != null && Combat.CurrentTarget.Oid != fighter.Oid && GetAggro(fighter.Oid).Hatred > GetAggro(Combat.CurrentTarget.Oid).Hatred)
            {
                //If no delayed ability cast - delayed abilities will handle this anyways!
                //if (CurTarget == null)
                //{
                    AI.Debugger?.SendClientMessage("[MR]: Switching target from "+Combat.CurrentTarget.Name + " to "+fighter.Name + ".");
                    AI.Debugger?.SendClientMessage("[MR]: Fighter hate: " + GetAggro(fighter.Oid).Hatred);

                    //This should take care of finding the proper target
                    Unit nextTarget = GetNextTarget();
                    Combat.SetTarget(nextTarget, TargetTypes.TARGETTYPES_TARGET_ENEMY);
                    if (nextTarget != null && _unit.IsKeepLord)
                        _unit.Say("Die " + nextTarget.Name + "!");
                    Chase(Combat.GetTarget(TargetTypes.TARGETTYPES_TARGET_ENEMY), true);
                //}
            }
        }
        public virtual void AddHealReceive(ushort oid, bool isPlayer, uint count)
        {
            AggroInfo info = GetAggro(oid);
            info.HealingReceived = count;
        }

        public virtual void OnTaunt(Unit taunter, byte lvl)
        {
            ulong maxHate = 0;

            if (_unit is Pet)
                return;

            AI.Debugger?.SendClientMessage("[MR]: Received taunt from  " + taunter.Name + ".");

            if (!Combat.IsInCombat)
                AI.ProcessCombatStart(taunter);
            else
            {
                AggroInfo info;
                foreach (KeyValuePair<ushort, AggroInfo> kp in Aggros)
                {
                    info = kp.Value;
                    ulong hate = info.GetHate();
                    if (hate > maxHate)
                        maxHate = hate;
                }

                uint newHatred = (uint)((300 + 1950 * ((lvl - 1)/39.0f)) * taunter.StsInterface.GetStatPercentageModifier(Stats.HateCaused));
                AddHatred(taunter, true, newHatred);
            }
        }

        public float GetDetaunt(ushort caster)
        {
            /*if (_unit.Detaunters.ContainsKey(caster))
            {
                float f = _unit.Detaunters[caster] / 100.0f;
                return f;
            }
            else
                return 1.0f;*/


            NewBuff buff = _unit.BuffInterface.GetBuff(1441, null);
            if (buff != null && caster == buff.Caster.Oid)
                return 0.5f;

            buff = _unit.BuffInterface.GetBuff(1516, null);
            if (buff != null && caster == buff.Caster.Oid)
                return 0.5f;

            buff = _unit.BuffInterface.GetBuff(1595, null);
            if (buff != null && caster == buff.Caster.Oid)
                return 0.5f;

            buff = _unit.BuffInterface.GetBuff(1753, null);
            if (buff != null && caster == buff.Caster.Oid)
                return 0.5f;

            buff = _unit.BuffInterface.GetBuff(1827, null);
            if (buff != null && caster == buff.Caster.Oid)
                return 0.5f;

            buff = _unit.BuffInterface.GetBuff(1915, null);
            if (buff != null && caster == buff.Caster.Oid)
                return 0.5f;

            buff = _unit.BuffInterface.GetBuff(1918, null);
            if (buff != null && caster == buff.Caster.Oid)
                return 0.5f;

            buff = _unit.BuffInterface.GetBuff(3471, null);
            if (buff != null && caster == buff.Caster.Oid)
                return 0.5f;

            buff = _unit.BuffInterface.GetBuff(3712, null);
            if (buff != null && caster == buff.Caster.Oid)
                return 0.75f;

            buff = _unit.BuffInterface.GetBuff(5436, null);
            if (buff != null && caster == buff.Caster.Oid)
                return 0.25f;

            buff = _unit.BuffInterface.GetBuff(5575, null);
            if (buff != null && caster == buff.Caster.Oid)
                return 0.5f;

            buff = _unit.BuffInterface.GetBuff(8088, null);
            if (buff != null && caster == buff.Caster.Oid)
                return 0.5f;

            buff = _unit.BuffInterface.GetBuff(8162, null);
            if (buff != null && caster == buff.Caster.Oid)
                return 0.5f;

            buff = _unit.BuffInterface.GetBuff(8245, null);
            if (buff != null && caster == buff.Caster.Oid)
                return 0.5f;

            buff = _unit.BuffInterface.GetBuff(8402, null);
            if (buff != null && caster == buff.Caster.Oid)
                return 0.5f;

            buff = _unit.BuffInterface.GetBuff(8477, null);
            if (buff != null && caster == buff.Caster.Oid)
                return 0.5f;

            buff = _unit.BuffInterface.GetBuff(8621, null);
            if (buff != null && caster == buff.Caster.Oid)
                return 0.5f;

            buff = _unit.BuffInterface.GetBuff(9089, null);
            if (buff != null && caster == buff.Caster.Oid)
                return 0.5f;

            buff = _unit.BuffInterface.GetBuff(9169, null);
            if (buff != null && caster == buff.Caster.Oid)
                return 0.5f;

            buff = _unit.BuffInterface.GetBuff(9256, null);
            if (buff != null && caster == buff.Caster.Oid)
                return 0.5f;

            buff = _unit.BuffInterface.GetBuff(9265, null);
            if (buff != null && caster == buff.Caster.Oid)
                return 0.5f;

            buff = _unit.BuffInterface.GetBuff(9392, null);
            if (buff != null && caster == buff.Caster.Oid)
                return 0.5f;

            buff = _unit.BuffInterface.GetBuff(9474, null);
            if (buff != null && caster == buff.Caster.Oid)
                return 0.5f;

            buff = _unit.BuffInterface.GetBuff(9555, null);
            if (buff != null && caster == buff.Caster.Oid)
                return 0.5f;

            buff = _unit.BuffInterface.GetBuff(10718, null);
            if (buff != null && caster == buff.Caster.Oid)
                return 0.5f;


            return 1.0f;
        }

        public virtual Unit GetNextTarget()
        {
            while (Aggros.Count > 0)
            {
                AggroInfo info = GetMaxAggroHate();

                Unit target = _unit.Region.GetObject(info.Oid) as Unit;

                if (target == null || target.IsDead || !target.IsInWorld() || info.Hatred == 0)
                {
                    Aggros.Remove(info.Oid);
                    continue;
                }

                return target;
            }

            return null;
        }

        public void NotifyTargetKilled(Unit victim)
        {
            AI.Debugger?.SendClientMessage("[MR]: Received kill notification for " + victim.Name + ".");
            Aggros.Remove(victim.Oid);

            if (Aggros.Count == 0)
                AI.ProcessCombatEnd();
        }

        #endregion
    }
}
