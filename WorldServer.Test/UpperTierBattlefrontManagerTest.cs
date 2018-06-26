﻿using System.Collections.Generic;
using Common;
using Common.Database.World.Battlefront;
using GameData;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorldServer.World.Battlefronts.Apocalypse;
using WorldServer.World.BattleFronts.Objectives;


namespace WorldServer.Test
{
    [TestClass]
    public class UpperTierBattleFrontManagerTest
    {
        public UpperTierBattleFrontManager manager { get; set; }
        public List<RVRProgression> SampleProgressionList { get; set; }
        public RegionMgr Region1 { get; set; }
        public RegionMgr Region3 { get; set; }
        public List<ApocBattlefieldObjective> PraagBOList { get; set; }
        public List<ApocBattlefieldObjective> ChaosWastesBOList { get; set; }
        public List<ApocBattlefieldObjective> ThunderMountainBOList { get; set; }
        public List<ApocBattlefieldObjective> KadrinValleyBOList { get; set; }

        public List<ApocBattlefieldObjective> Region1BOList { get; set; }
        public List<ApocBattlefieldObjective> Region3BOList { get; set; }
        public List<RegionMgr> RegionMgrs { get; set; }

        [TestInitialize]
        public void Setup()
        {

            RegionMgrs = new List<RegionMgr>();

            PraagBOList = new List<ApocBattlefieldObjective>();
            ChaosWastesBOList= new List<ApocBattlefieldObjective>();
            ThunderMountainBOList= new List<ApocBattlefieldObjective>();
            KadrinValleyBOList = new List<ApocBattlefieldObjective>();

            Region1BOList = new List<ApocBattlefieldObjective>();
            Region3BOList = new List<ApocBattlefieldObjective>();


            var R1ZoneList = new List<Zone_Info>();
            R1ZoneList.Add(new Zone_Info { ZoneId = 200, Name = "R1Zone200 PR", Pairing = 2});
            R1ZoneList.Add(new Zone_Info { ZoneId = 201, Name = "R1Zone201 CW", Pairing = 2 });

            var R3ZoneList = new List<Zone_Info>();
            R3ZoneList.Add(new Zone_Info { ZoneId = 400, Name = "R3Zone400 TM", Pairing =1 });
            R3ZoneList.Add(new Zone_Info { ZoneId = 401, Name = "R3Zone401 KV", Pairing = 1 });

            Region1 = new RegionMgr(1, R1ZoneList, "Region1");
            Region3 = new RegionMgr(3, R3ZoneList, "Region3");

            Region1.ndbf =  new ApocBattleFront(Region1, Region1BOList, new HashSet<Player>(), manager);
            Region3.ndbf = new ApocBattleFront(Region3, Region3BOList, new HashSet<Player>(), manager);

            RegionMgrs.Add(Region1);
            RegionMgrs.Add(Region3);


            PraagBOList.Add(new ApocBattlefieldObjective(1, "BO1", 200, 1, 4));
            PraagBOList.Add(new ApocBattlefieldObjective(2, "BO2", 200, 1, 4));
            PraagBOList.Add(new ApocBattlefieldObjective(3, "BO3", 200, 1, 4));
            PraagBOList.Add(new ApocBattlefieldObjective(4, "BO4", 200, 1, 4));

            ChaosWastesBOList.Add(new ApocBattlefieldObjective(11, "BO1", 201, 1, 4));
            ChaosWastesBOList.Add(new ApocBattlefieldObjective(12, "BO2", 201, 1, 4));
            ChaosWastesBOList.Add(new ApocBattlefieldObjective(13, "BO3", 201, 1, 4));
            ChaosWastesBOList.Add(new ApocBattlefieldObjective(14, "BO4", 201, 1, 4));

            ThunderMountainBOList.Add(new ApocBattlefieldObjective(21, "BO1", 400, 3, 4));
            ThunderMountainBOList.Add(new ApocBattlefieldObjective(22, "BO2", 400, 3, 4));
            ThunderMountainBOList.Add(new ApocBattlefieldObjective(23, "BO3", 400, 3, 4));
            ThunderMountainBOList.Add(new ApocBattlefieldObjective(24, "BO4", 400, 3, 4));

            KadrinValleyBOList.Add(new ApocBattlefieldObjective(31, "BO1", 401, 3, 4));
            KadrinValleyBOList.Add(new ApocBattlefieldObjective(32, "BO2", 401, 3, 4));
            KadrinValleyBOList.Add(new ApocBattlefieldObjective(33, "BO3", 401, 3, 4));
            KadrinValleyBOList.Add(new ApocBattlefieldObjective(34, "BO4", 401, 3, 4));

            Region1BOList.AddRange(PraagBOList);
            Region1BOList.AddRange(ChaosWastesBOList);

            Region3BOList.AddRange(ThunderMountainBOList);
            Region3BOList.AddRange(KadrinValleyBOList);


            SampleProgressionList = new List<RVRProgression>();
            SampleProgressionList.Add(new RVRProgression
            {
                Tier = 4,
                ZoneId = 200,
                BattleFrontId = 1,
                Description = "Praag", // named for default pickup
                DestWinProgression = 2,
                OrderWinProgression = 3,
                PairingId = 2,
                RegionId = 1
            });
            SampleProgressionList.Add(new RVRProgression
            {
                Tier = 4,
                ZoneId = 201,
                BattleFrontId = 2,
                Description = "Chaos Wastes",
                DestWinProgression = 6,
                OrderWinProgression = 7,
                PairingId = 2,
                RegionId = 1
            });
            SampleProgressionList.Add(new RVRProgression
            {
                Tier = 4,
                ZoneId = 400,
                BattleFrontId = 6,
                Description = "Thunder Mountain",
                DestWinProgression = 7,
                OrderWinProgression = 2,
                PairingId = 1,
                RegionId = 3
            });
            SampleProgressionList.Add(new RVRProgression
            {
                Tier = 4,
                ZoneId = 401,
                BattleFrontId = 7,
                Description = "Kadrin Valley",
                DestWinProgression = 1,
                OrderWinProgression = 1,
                PairingId = 1,
                RegionId = 3
            });
            manager = new UpperTierBattleFrontManager(SampleProgressionList, RegionMgrs);
        }

        [TestMethod]
        public void Constructor_NoPairings_CreatesError()
        {
            var manager = new UpperTierBattleFrontManager(null, RegionMgrs);
            Assert.IsNull(manager.ActiveBattleFront);
        }

        [TestMethod]
        public void Constructor_NoActivePairings_CreatesError()
        {
            var manager = new UpperTierBattleFrontManager(SampleProgressionList, RegionMgrs);
            Assert.IsNull(manager.ActiveBattleFront);
        }

        [TestMethod]
        public void ResetActivePairing()
        {
            var manager = new UpperTierBattleFrontManager(SampleProgressionList, RegionMgrs);
            var bf = manager.ResetBattleFrontProgression();
            Assert.IsTrue(bf.BattleFrontId == 1);
        }

        [TestMethod]
        public void ActivePairingLocated()
        {

            var manager = new UpperTierBattleFrontManager(SampleProgressionList, RegionMgrs);
            var bf = manager.ResetBattleFrontProgression();
            Assert.IsTrue(bf.DestWinProgression == 2);

            bf = manager.AdvanceBattleFront(Realms.REALMS_REALM_DESTRUCTION);
            Assert.IsTrue(bf.BattleFrontId == 2);
            Assert.IsTrue(bf.DestWinProgression == 6);
            Assert.IsTrue(bf.OrderWinProgression == 7);
            Assert.IsTrue(manager.ActiveBattleFront.BattleFrontId == 2);

            bf = manager.AdvanceBattleFront(Realms.REALMS_REALM_DESTRUCTION);
            Assert.IsTrue(bf.BattleFrontId == 6);
            Assert.IsTrue(bf.DestWinProgression == 1);
            Assert.IsTrue(bf.OrderWinProgression == 2);
            Assert.IsTrue(manager.ActiveBattleFront.BattleFrontId == 6);

            bf = manager.AdvanceBattleFront(Realms.REALMS_REALM_ORDER);
            Assert.IsTrue(bf.BattleFrontId == 2);
            Assert.IsTrue(bf.DestWinProgression == 6);
            Assert.IsTrue(bf.OrderWinProgression == 7);
            Assert.IsTrue(manager.ActiveBattleFront.BattleFrontId == 2);

            bf = manager.AdvanceBattleFront(Realms.REALMS_REALM_DESTRUCTION);
            Assert.IsTrue(bf.BattleFrontId == 6);
            Assert.IsTrue(bf.DestWinProgression == 1);
            Assert.IsTrue(bf.OrderWinProgression == 2);
            Assert.IsTrue(manager.ActiveBattleFront.BattleFrontId == 6);

            bf = manager.AdvanceBattleFront(Realms.REALMS_REALM_DESTRUCTION);
            Assert.IsTrue(bf.BattleFrontId == 1);
            Assert.IsTrue(bf.DestWinProgression == 2);
            Assert.IsTrue(bf.OrderWinProgression == 3);

            Assert.IsTrue(manager.ActiveBattleFront.BattleFrontId == 1);
            Assert.IsTrue(manager.ActiveBattleFront.DestWinProgression == 2);
            Assert.IsTrue(manager.ActiveBattleFront.OrderWinProgression == 3);
        }

        [TestMethod]
        public void OpenActiveBattleFrontSetsCorrectBOFlags()
        {

            // Emp/Chaos battleFront
            var manager = new UpperTierBattleFrontManager(SampleProgressionList, RegionMgrs);
            var bf = manager.ResetBattleFrontProgression();
            var bfEmpireChaos = new ApocBattleFront(Region1, Region1BOList, new HashSet<Player>(), manager);

            manager.OpenActiveBattlefront();
            Assert.IsTrue(manager.ActiveBattleFront.BattleFrontId == 1); // Praag

            // Ensure that the BOs for this battlefront ONLY are unlocked.
            foreach (var bo in bfEmpireChaos.Objectives)
            {
                if (bo.ZoneId == 200)
                {
                    // Locking the Region should set all BOs in the region to be zonelocked (
                    Assert.IsFalse(bo.FlagState == ObjectiveFlags.ZoneLocked);
                }
                else
                {
                    Assert.IsTrue(bo.FlagState == ObjectiveFlags.ZoneLocked);
                }
            }
        }

        [TestMethod]
        public void LockRegion1()
        {
            // Emp/Chaos battleFront
            var manager = new UpperTierBattleFrontManager(SampleProgressionList, RegionMgrs);
            var bf = manager.ResetBattleFrontProgression();
            var bfEmpireChaos = new ApocBattleFront(Region1, Region1BOList, new HashSet<Player>(), manager);

            manager.OpenActiveBattlefront();
            Assert.IsTrue(manager.ActiveBattleFront.BattleFrontId == 1); // Praag

            manager.LockBattleFront(Realms.REALMS_REALM_DESTRUCTION);

            // Ensure battlefront 1 is locked and to Destro
            Assert.IsTrue(bfEmpireChaos.LockingRealm == Realms.REALMS_REALM_DESTRUCTION);
            Assert.IsTrue(bfEmpireChaos.VictoryPointProgress.DestructionVictoryPoints == BattleFrontConstants.LOCK_VICTORY_POINTS);

            // BF has progressed
            Assert.IsTrue(manager.ActiveBattleFront.BattleFrontId == 2);
            Assert.IsTrue(bfEmpireChaos.LockingRealm == Realms.REALMS_REALM_NEUTRAL);
            Assert.IsTrue(bfEmpireChaos.VictoryPointProgress.DestructionVictoryPoints == 0);
            Assert.IsTrue(bfEmpireChaos.VictoryPointProgress.OrderVictoryPoints == 0);

            // Ensure that the BOs for this battlefront ONLY are locked.
            foreach (var apocBattlefieldObjective in bfEmpireChaos.Objectives)
            {
                // Locking a battlefront should ZoneLock the BOs in that Zone, and Open those in the next battlefront.
                if (apocBattlefieldObjective.ZoneId == 200)
                {
                    // Should be all locked.

                    Assert.IsTrue(apocBattlefieldObjective.State == StateFlags.ZoneLocked);
                }
                else
                {
                    // Next BF
                    if (apocBattlefieldObjective.ZoneId == 201)
                    {
                        Assert.IsTrue(apocBattlefieldObjective.State == StateFlags.Unsecure);
                    }
                }
            }
        }
    }
}