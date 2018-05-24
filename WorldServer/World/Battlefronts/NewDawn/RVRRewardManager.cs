﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FrameWork;
using GameData;
using NLog;

namespace WorldServer.World.Battlefronts.NewDawn
{
    /// <summary>
    /// Manages rewards from RVR mechanic.
    /// </summary>
    public class RVRRewardManager
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        public Dictionary<uint, XpRenown> DelayedRewards = new Dictionary<uint, XpRenown>();
        public float RewardScaleMultiplier { get; set; }

        public RVRRewardManager()
        {
            _logger.Trace($"CTOR RVRRewardManager");
        }

        
        /// <summary>
        /// Add delayed XPR rewards for kills in RVR. 
        /// </summary>
        /// <param name="killer"></param>
        /// <param name="killed"></param>
        /// <param name="xpShare"></param>
        /// <param name="renownShare"></param>
        public void AddDelayedRewardsFrom(Player killer, Player killed, uint xpShare, uint renownShare)
        {
            if (xpShare == 0 && renownShare == 0)
                return;

            XpRenown xprEntry;

            uint renownReward = (uint)(renownShare * killer.GetKillRewardScaler(killed));

#if BATTLEFRONT_DEBUG
            player.SendClientMessage($"{ObjectiveName} storing {xpShare} XP and {renownReward} renown");
#endif
            DelayedRewards.TryGetValue(killer.CharacterId, out xprEntry);

            // Character has no record of XP/RR gain.
            if (xprEntry == null)
            {
                xprEntry = new XpRenown(xpShare, renownReward, 0, 0, TCPManager.GetTimeStamp());
                DelayedRewards.Add(killer.CharacterId, xprEntry);
            }
            else
            {
                xprEntry.XP += xpShare;
                xprEntry.Renown += renownReward;
                xprEntry.LastUpdatedTime = TCPManager.GetTimeStamp();
            }
            _logger.Debug($"Delayed Rewards for Player : {killer.Name}  - {xprEntry.ToString()}");
        }

        //public float CalculateRewardScaleMultipler(int closeOrderCount, int closeDestroCount, Realms owningRealm, int secureProgress, float objectiveRewardScaler)
        //{
          

        //    int playerCount;
        //    ushort influenceId;
        //    float scaleMult;
        //    if (owningRealm == Realms.REALMS_REALM_ORDER)
        //    {
        //        playerCount = closeOrderCount;
        //        scaleMult = 1;
        //    }
        //    else
        //    {
        //        playerCount = closeDestroCount;
        //        scaleMult = 1;
        //    }

        //    // No multiplier based on population
        //    scaleMult *= Region.ndbf.GetObjectiveRewardScaler(OwningRealm, playerCount);

        //    return scaleMult;
        //}


        /// <summary>
        ///     Grants a small reward to all players in close range for defending.
        /// </summary>
        /// <remarks>Invoked in short periods of time</remarks>
        public VictoryPoint RewardCaptureTick(ISet<Player> playersWithinRange, Realms owningRealm, int tier, string objectiveName, float rewardScaleMultiplier)
        {
            ushort influenceId;

            _logger.Trace($"Objective {objectiveName} has {playersWithinRange} players nearby");

            // Because of the Field of Glory buff, the XP value here is doubled.
            // The base reward in T4 is therefore 3000 XP.
            // Population scale factors can up this to 9000 if the region is full of people and then raise or lower it depending on population balance.
            var baseXp = BattlefrontConstants.FLAG_SECURE_REWARD_SCALER * tier * rewardScaleMultiplier;
            var baseRp = baseXp / 10f;
            var baseInf = baseRp / 2.5f;

            foreach (var player in playersWithinRange)
            {
                if (player.Realm != owningRealm || player.CbtInterface.IsInCombat || player.IsAFK || player.IsAutoAFK)
                    continue;

                // Bad dirty hak by Hargrim to fix Area Influence bug
                if (owningRealm == Realms.REALMS_REALM_ORDER)
                    influenceId = (ushort)player.CurrentArea.OrderInfluenceId;
                else
                    influenceId = (ushort)player.CurrentArea.DestroInfluenceId;


                var xp = Math.Max((uint)baseXp, 1);
                var rr = Math.Max((uint)baseRp, 1);
                var inf = Math.Max((ushort)baseInf, (ushort)1);

                player.AddXp(Math.Max((uint)baseXp, 1), false, false);
                player.AddRenown(Math.Max((uint)baseRp, 1), false, RewardType.ObjectiveCapture, objectiveName);
                player.AddInfluence(influenceId, Math.Max((ushort)baseInf, (ushort)1));
                
                // TODO
                //Battlefront.AddContribution(player, (uint)baseRp);

                _logger.Trace($"Player:{player.Name} ScaleMult:{rewardScaleMultiplier} XP:{xp} RR:{rr} INF:{inf}");
            }

            // Returns 2 VP per tick for locking realm, -1 for non locking realm.
            if (owningRealm == Realms.REALMS_REALM_ORDER)
            {
                _logger.Debug($"Order-held {objectiveName} +2 Order -1 Dest");
                return new VictoryPoint(2, -1);
            }
            else
            {
                _logger.Debug($"Dest-held {objectiveName} +2 Dest -1 Order");
                return new VictoryPoint(-1, 2);
            }
        }
    }
}
