﻿using System;
using GameData;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorldServer.World.Battlefronts.NewDawn;

namespace WorldServer.Test
{
    [TestClass]
    public class CalculateObjectiveRewardScaleTest
    {
        [TestMethod]
        public void EqualPlayerNumbers()
        {
            var ndBO = new NewDawnBattlefieldObjective();

            var objectiveMultiplierDest = ndBO.CalculateObjectiveRewardScale(Realms.REALMS_REALM_DESTRUCTION, 10, 10);

            Assert.IsTrue(objectiveMultiplierDest == 0);

            var objectiveMultiplierNeut = ndBO.CalculateObjectiveRewardScale(Realms.REALMS_REALM_NEUTRAL, 10, 10);

            Assert.IsTrue(objectiveMultiplierNeut == 0);

            var objectiveMultiplierOrder = ndBO.CalculateObjectiveRewardScale(Realms.REALMS_REALM_ORDER, 10, 10);

            Assert.IsTrue(objectiveMultiplierOrder == 0);

            var objectiveMultiplierSomethingElse = ndBO.CalculateObjectiveRewardScale(Realms.REALMS_REALM_HOSTILE, 10, 10);

            Assert.IsTrue(objectiveMultiplierSomethingElse == 0);
        }

        [TestMethod]
        public void SmallDestroDefendingBO()
        {
            var ndBO = new NewDawnBattlefieldObjective();

            var objectiveMultiplierDest = ndBO.CalculateObjectiveRewardScale(Realms.REALMS_REALM_DESTRUCTION, 50, 5);

            Assert.IsTrue(objectiveMultiplierDest == 9.0f);
        }

        [TestMethod]
        public void SmallOrderDefendingBO()
        {
            var ndBO = new NewDawnBattlefieldObjective();

            var objectiveMultiplierOrder = ndBO.CalculateObjectiveRewardScale(Realms.REALMS_REALM_ORDER, 5, 50);

            Assert.IsTrue(objectiveMultiplierOrder == 9.0f);
        }

        [TestMethod]
        public void SmallDestroAttackingBO()
        {
            var ndBO = new NewDawnBattlefieldObjective();

            var objectiveMultiplierDest = ndBO.CalculateObjectiveRewardScale(Realms.REALMS_REALM_DESTRUCTION, 5, 50);

            Assert.IsTrue(objectiveMultiplierDest == 1f);
        }

        [TestMethod]
        public void SmallOrderAttackingBO()
        {
            var ndBO = new NewDawnBattlefieldObjective();

            var objectiveMultiplierOrder = ndBO.CalculateObjectiveRewardScale(Realms.REALMS_REALM_ORDER, 50, 5);

            Assert.IsTrue(objectiveMultiplierOrder == 1f);
        }

    }
}
