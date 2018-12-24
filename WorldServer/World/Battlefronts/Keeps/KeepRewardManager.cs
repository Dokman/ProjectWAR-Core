﻿using Common;
using GameData;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using SystemData;
using WorldServer.Services.World;
using WorldServer.World.Battlefronts.Bounty;
using WorldServer.World.BattleFronts.Keeps;

namespace WorldServer.World.Battlefronts.Keeps
{
    public static class KeepRewardManager
    {
        private static readonly Logger RewardLogger = LogManager.GetLogger("RewardLogger");

        public static void OuterDoorReward(List<Player> playersInRange, string description, ContributionManager contributionManagerInstance)
        {
            // Small reward for outer door destruction
            foreach (var player in playersInRange)
            {
                if (!player.Initialized)
                    continue;

                var rnd = new Random();
                var random = rnd.Next(1, 25);

                player.AddXp((uint)(1000 * (1 + random / 100)), false, false);
                player.AddRenown((uint)(200 * (1 + random / 100)), false, RewardType.ObjectiveCapture, description);

                // Add contribution
                contributionManagerInstance.UpdateContribution(player.CharacterId, (byte)ContributionDefinitions.DESTROY_OUTER_DOOR);
                var contributionDefinition = new BountyService().GetDefinition((byte)ContributionDefinitions.DESTROY_OUTER_DOOR);
                player.BountyManagerInstance.AddCharacterBounty(player.CharacterId, contributionDefinition.ContributionValue);
            }
        }

        public static void InnerDoorReward(List<Player> playersInRange, string description, ContributionManager contributionManagerInstance)
        {
            // Small reward for inner door destruction
            foreach (var player in playersInRange)
            {
                if (!player.Initialized)
                    continue;
                var rnd = new Random();
                var random = rnd.Next(1, 25);
                player.AddXp((uint)(1500 * (1 + random / 100)), false, false);
                player.AddRenown((uint)(400 * (1 + random / 100)), false, RewardType.ObjectiveCapture, description);

                // Add contribution
                contributionManagerInstance.UpdateContribution(player.CharacterId, (byte)ContributionDefinitions.DESTROY_INNER_DOOR);
                var contributionDefinition = new BountyService().GetDefinition((byte)ContributionDefinitions.DESTROY_INNER_DOOR);
                player.BountyManagerInstance.AddCharacterBounty(player.CharacterId, contributionDefinition.ContributionValue);
            }
        }

        public static void DefenceTickReward(BattleFrontKeep keep, List<Player> playersInRange, string description, ContributionManager contributionManagerInstance)
        {
            foreach (var plr in playersInRange)
            {
                if (keep.Realm == plr.Realm && plr.ValidInTier(keep.Tier, true))
                {
                    var influenceId = plr.Realm == Realms.REALMS_REALM_DESTRUCTION ? plr.CurrentArea.DestroInfluenceId : plr.CurrentArea.OrderInfluenceId;

                    var totalXp = 2000 * keep.Tier;
                    var totalRenown = 300 * keep.Tier;
                    var totalInfluence = 100 * keep.Tier;

                    if (keep.PlayersKilledInRange < 4 * keep.Tier)
                    {
                        totalXp += (int)(totalXp * (0.25 + keep.PlayersKilledInRange / 40f * 0.75));
                        totalRenown += (int)(totalRenown * (0.25 + keep.PlayersKilledInRange / 40f * 0.75));
                        totalInfluence += (int)(totalInfluence * (0.25 + keep.PlayersKilledInRange / 40f * 0.75));
                    }

                    plr.AddXp((uint)totalXp, false, false);
                    plr.AddRenown((uint)totalRenown, false, RewardType.ObjectiveDefense, description);
                    plr.AddInfluence((ushort)influenceId, (ushort)totalInfluence);

                    plr.SendClientMessage($"You've received a reward for your contribution to the holding of {keep.Info.Name}.", ChatLogFilters.CHATLOGFILTERS_RVR);

                    // Add Contribution for Keep Defence Tick
                    plr.UpdatePlayerBountyEvent((byte)ContributionDefinitions.KEEP_DEFENCE_TICK);

                    RewardLogger.Info("Keep", $"Keep Defence XP : {totalXp} RP: {totalRenown}, Influence: {totalInfluence}");
                }
            }
        }

        public static void KeepLordKill(BattleFrontKeep keep, List<Player> playersInRange, string description, ContributionManager contributionManagerInstance, IEnumerable<KeyValuePair<uint, int>> eligiblePlayers)
        {
            foreach (var plr in playersInRange)
            {
                // Add contribution for being in range
                contributionManagerInstance.UpdateContribution(plr.CharacterId, (byte)ContributionDefinitions.KILL_KEEP_LORD);
                var contributionDefinition = new BountyService().GetDefinition((byte)ContributionDefinitions.KILL_KEEP_LORD);
                plr.BountyManagerInstance.AddCharacterBounty(plr.CharacterId, contributionDefinition.ContributionValue);

                if (plr.PriorityGroup?.GetLeader() == plr)
                {
                    contributionManagerInstance.UpdateContribution(plr.CharacterId, (byte)ContributionDefinitions.GROUP_LEADER_KILL_KEEP_LORD);
                    contributionDefinition = new BountyService().GetDefinition((byte)ContributionDefinitions.GROUP_LEADER_KILL_KEEP_LORD);
                    plr.BountyManagerInstance.AddCharacterBounty(plr.CharacterId, contributionDefinition.ContributionValue);
                }
            }


            RewardLogger.Info("**********************KEEP FLIP******************************");
            RewardLogger.Info($"Distributing rewards for Keep {description}");
            RewardLogger.Info("*************************************************************");

            uint influenceId = 0;

            byte objCount = 0;

            var battlePenalty = false;

            var totalXp = 800 * keep.Tier + 200 * keep.Tier * objCount + keep.PlayersKilledInRange * keep.Tier * 30; // Field of Glory, reduced
            var totalRenown = 250 * keep.Tier + 80 * keep.Tier * objCount + keep.PlayersKilledInRange * 80; // Ik : Increased values here.
            var totalInfluence = 40 * keep.Tier + 20 * keep.Tier * objCount + keep.PlayersKilledInRange * keep.Tier * 6;

            if (keep.PlayersKilledInRange < 4 * keep.Tier)
            {
                battlePenalty = true;

                totalXp = (int)(totalXp * (0.25 + keep.PlayersKilledInRange / 40f * 0.75));
                totalRenown = (int)(totalRenown * (0.25 + keep.PlayersKilledInRange / 40f * 0.75));
                totalInfluence = (int)(totalInfluence * (0.25 + keep.PlayersKilledInRange / 40f * 0.75));
            }

            RewardLogger.Info($"Lock XP : {totalXp} RP: {totalRenown}, Influence: {totalInfluence}");
            try
            {
                RewardLogger.Info($"Processing {eligiblePlayers.Count()} players for Keep lock rewards");

                foreach (var characterId in eligiblePlayers)
                {
                    var player = Player.GetPlayer(characterId.Key);

                    if (player == null)
                        continue;

                    if (!player.Initialized)
                        continue;

                    if (player.ValidInTier(keep.Tier, true))
                        player.QtsInterface.HandleEvent(Objective_Type.QUEST_CAPTURE_KEEP, keep.Info.KeepId, 1);

                    RewardLogger.Debug($"Player {player.Name} is valid");

                    if (influenceId == 0)
                        influenceId = player.Realm == Realms.REALMS_REALM_DESTRUCTION ? player.CurrentArea.DestroInfluenceId : player.CurrentArea.OrderInfluenceId;

                    player.AddXp((uint)totalXp, false, false);
                    player.AddRenown((uint)totalRenown, false, RewardType.ZoneKeepCapture, keep.Info.Name);
                    player.AddInfluence((ushort)influenceId, (ushort)totalInfluence);

                    if (battlePenalty)
                        player.SendClientMessage("This keep was taken with little to no resistance. The rewards have therefore been reduced.");

                    RewardLogger.Info($"Distributing rewards for Keep {keep.Info.Name} to {player.Name} RR:{totalRenown} INF:{totalInfluence}");
                    RewardLogger.Info($"Distributing rewards for Keep {keep.Info.Name} to {player.Name} RR:{totalRenown} INF:{totalInfluence}");
                }

                keep.PlayersKilledInRange = 0;
            }
            catch (Exception e)
            {
                RewardLogger.Error($"Exception distributing rewards for Keep take {e.Message} {e.StackTrace}");
                throw;
            }
        }

    }
}
