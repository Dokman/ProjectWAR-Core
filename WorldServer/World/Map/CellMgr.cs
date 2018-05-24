﻿//#define SUPPRESS_LOAD

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Common;
using FrameWork;
using Common.Database.World.Maps;

namespace WorldServer
{
    public class CellMgr
    {
        public RegionMgr Region;
        public ushort X;
        public ushort Y;
        public CellSpawns Spawns;

        public CellMgr(RegionMgr mgr, ushort offX, ushort offY)
        {
            Region = mgr;
            X = offX;
            Y = offY;
            Spawns = mgr.GetCellSpawn(offX,offY);
        }

        #region Objects

        public List<Object> Objects = new List<Object>();
        public List<Player> Players = new List<Player>();

        public void AddObject(Object obj)
        {
            if (obj is Player)
            {
                Players.Add((Player)obj);
                Region.LoadCells(X, Y, 1); // Load nearby cells when a player enters
            }

           Objects.Add(obj);
           obj._Cell = this;
        }
        public void RemoveObject(Object obj)
        {
            //Log.Success("RemoveObject", "[" + X + "," + Y + "] Cell Remove " + Obj.Name);

            if (obj._Cell == this)
            {
                if (obj.IsPlayer())
                    Players.Remove(obj.GetPlayer());

                Objects.Remove(obj);
                obj._Cell = null;
            }
        }

        #endregion

        #region Spawns

        public bool Loaded;
        public void Load()
        {
            lock (this)
            {
                if (Loaded)
                    return;

                Loaded = true;
            }

            Log.Debug(ToString(), "Loading... ");

            #if !DEBUG || !SUPPRESS_LOAD
            foreach (Creature_spawn spawn in Spawns.CreatureSpawns)
                Region.CreateCreature(spawn);

            foreach (GameObject_spawn spawn in Spawns.GameObjectSpawns)
                Region.CreateGameObject(spawn);

            foreach (Chapter_Info spawn in Spawns.ChapterSpawns)
                Region.CreateChapter(spawn);

            foreach (PQuest_Info quest in Spawns.PublicQuests)
                Region.CreatePQuest(quest);
            #endif
    }

        public override string ToString()
        {
            return "CellMgr["+X+","+Y+"]";
        }

        #endregion
    }
}
