﻿using Common.Database.World.Battlefront;
using System;
using Common;
using WorldServer.Services.World;

namespace WorldServer.World.Battlefronts.Objectives
{
    /// <summary>
    /// Game object representing a portal around an objective
    /// allowing port to warcamp.
    /// </summary>
    class PortalBase : GameObject
    {
        private const uint PORTAL_PROTO_ENTRY = 242;
        private const uint PORTAL_DISPLAY_ID = 1583;

        private Random random = new Random();

        internal PortalBase(BattlefrontObject origin)
        {
            Spawn = CreateSpawn(origin);
        }

        internal PortalBase(GameObject_spawn spawn)
        {
            Spawn = spawn;
        }

        /// <summary>
        /// Creates a game object spawn entity from given battlefront portal object.
        /// </summary>
        /// <param name="battlefrontObject">Portal object providing raw data</param>
        /// <returns>newly created spawn entity</returns>
        private GameObject_spawn CreateSpawn(BattlefrontObject battlefrontObject)
        {
            GameObject_proto proto = GameObjectService.GetGameObjectProto(PORTAL_PROTO_ENTRY);
            proto = (GameObject_proto)proto.Clone();

            GameObject_spawn spawn = new GameObject_spawn();
            spawn.BuildFromProto(proto);

            // boule blanche : 3457
            // grosse boule blanche : 1675
            proto.Scale = 25;
            // spawn.DisplayID = 1675;
            spawn.DisplayID = 1675;
            spawn.ZoneId = battlefrontObject.ZoneId;

            Point3D worldPos = GetWorldPosition(battlefrontObject);
            spawn.WorldX = worldPos.X;
            spawn.WorldY = worldPos.Y;
            spawn.WorldZ = worldPos.Z;
            spawn.WorldO = battlefrontObject.O;

            return spawn;
        }

        protected Point3D GetWorldPosition(BattlefrontObject bObject)
        {
            Zone_Info zone = ZoneService.GetZone_Info(bObject.ZoneId);
            return ZoneService.GetWorldPosition(zone, (ushort)bObject.X, (ushort)bObject.Y, (ushort)bObject.Z);
        }

        protected void Teleport(Player player, BattlefrontObject target, Point3D targetPos)
        {
            Point2D randomPoint = targetPos.GetPointFromHeading((ushort)random.Next(0, 4096), 5);
            player.Teleport(target.ZoneId, (uint)randomPoint.X, (uint)randomPoint.Y, (ushort)targetPos.Z, (ushort)target.O);
        }

    }
}
