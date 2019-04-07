﻿using GameData;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using WorldServer.Managers;
using WorldServer.World.Battlefronts.Bounty;
using WorldServer.World.Objects;

namespace WorldServer.World.Battlefronts.Apocalypse
{
    public static class PlayerUtil
    {
        public static int GetTotalPVPPlayerCountInRegion(int regionId)
        {
            lock (Player._Players)
            {
                return Player._Players.Count(x => !x.IsDisposed && x.IsInWorld() && x != null && x.Region.RegionId == regionId && x.CbtInterface.IsPvp);
            }
        }

        public static int GetTotalDestPVPPlayerCountInRegion(int regionId)
        {
            lock (Player._Players)
            {
                return Player._Players.Count(x => x.Realm == Realms.REALMS_REALM_DESTRUCTION && !x.IsDisposed && x.IsInWorld() && x != null && x.Region.RegionId == regionId && x.CbtInterface.IsPvp);
            }
        }

        public static int GetTotalOrderPVPPlayerCountInRegion(int regionId)
        {
            lock (Player._Players)
            {
                return Player._Players.Count(x => x.Realm == Realms.REALMS_REALM_ORDER && !x.IsDisposed && x.IsInWorld() && x != null && x.Region.RegionId == regionId && x.CbtInterface.IsPvp);
            }
        }

        public static int GetTotalDestPVPPlayerCountInZone(int zoneID)
        {
            lock (Player._Players)
            {
                return Player._Players.Count(x => x.Realm == Realms.REALMS_REALM_DESTRUCTION && !x.IsDisposed && x.IsInWorld() && !x.IsAFK && !x.IsAutoAFK && x != null && x.ZoneId == zoneID && x.CbtInterface.IsPvp);
            }
        }

        public static int GetTotalOrderPVPPlayerCountInZone(int zoneID)
        {
            lock (Player._Players)
            {
                return Player._Players.Count(x => x.Realm == Realms.REALMS_REALM_ORDER && !x.IsDisposed && x.IsInWorld() && !x.IsAFK && !x.IsAutoAFK && x != null && x.ZoneId == zoneID && x.CbtInterface.IsPvp);
            }
        }


        public static List<Player> GetOrderPlayersInZone(int zoneId)
        {
            lock (Player._Players)
            {
                return Player._Players.Where(x => x.Realm == Realms.REALMS_REALM_ORDER && !x.IsDisposed && x.IsInWorld() && !x.IsAFK && !x.IsAutoAFK && x != null && x.ZoneId == zoneId && x.CbtInterface.IsPvp).ToList();
            }
        }

        public static List<Player> GetDestPlayersInZone(int zoneId)
        {
            lock (Player._Players)
            {
                return Player._Players.Where(x => x.Realm == Realms.REALMS_REALM_DESTRUCTION && !x.IsDisposed && x.IsInWorld() && !x.IsAFK && !x.IsAutoAFK && x != null && x.ZoneId == zoneId && x.CbtInterface.IsPvp).ToList();
            }
        }

        public static List<Player> GetAllFlaggedPlayersInZone(int zoneId)
        {
            lock (Player._Players)
            {
                return Player._Players.Where(x => !x.IsDisposed && x.IsInWorld() && !x.IsAFK && !x.IsAutoAFK && x != null && x.ZoneId == zoneId && x.CbtInterface.IsPvp).ToList();
            }
        }

        /// <summary>
        /// Given contributing players and their contributions, split out the eligible, the contributing winning realm and contributing losing realm players.
        /// </summary>
        /// <param name="allContributingPlayers"></param>
        /// <param name="lockingRealm"></param>
        /// <param name="contributionManager"></param>
        /// <param name="updateHonor"></param>
        /// <param name="updateAnalytics"></param>
        /// <returns></returns>
        public static Tuple<ConcurrentDictionary<Player, int>, ConcurrentDictionary<Player, int>, ConcurrentDictionary<Player, int>>
            SegmentEligiblePlayers(
                IEnumerable<KeyValuePair<uint, int>> allContributingPlayers, Realms lockingRealm, ContributionManager contributionManager, bool updateHonor = true, bool updateAnalytics = true)
        {
            var winningRealmPlayers = new ConcurrentDictionary<Player, int>();
            var losingRealmPlayers = new ConcurrentDictionary<Player, int>();
            var allEligiblePlayerDictionary = new ConcurrentDictionary<Player, int>();


            // Partition the players by winning realm. 
            foreach (var contributingPlayer in allContributingPlayers)
            {
                var player = Player.GetPlayer(contributingPlayer.Key);
                if (player != null)
                {
                    if (updateHonor)
                    {
                        // Update the Honor Points of the Contributing Players
                        player.Info.HonorPoints += (ushort)contributingPlayer.Value;
                        CharMgr.Database.SaveObject(player.Info);
                    }

                    if (player.Realm == lockingRealm)
                    {
                        winningRealmPlayers.TryAdd(player, contributingPlayer.Value);
                    }
                    else
                    {
                        losingRealmPlayers.TryAdd(player, contributingPlayer.Value);
                    }

                    allEligiblePlayerDictionary.TryAdd(player, contributingPlayer.Value);

                    if (updateAnalytics)
                    {
                        // Get the contribution list for this player
                        var contributionDictionary =
                            contributionManager.GetContributionStageDictionary(contributingPlayer.Key);
                        // Record the contribution types and values for the player for analytics
                        PlayerContributionManager.RecordContributionAnalytics(player, contributionDictionary);
                    }

                }
            }

            return new Tuple<ConcurrentDictionary<Player, int>, ConcurrentDictionary<Player, int>, ConcurrentDictionary<Player, int>>(allEligiblePlayerDictionary, winningRealmPlayers, losingRealmPlayers);

        }
    }
}
