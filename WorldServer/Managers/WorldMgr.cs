﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using SystemData;
using Common;
using FrameWork;
using GameData;
using Common.Database.World.Battlefront;
using WorldServer.World.Battlefronts.Keeps;
using WorldServer.Scenarios;
using WorldServer.Services.World;
using Common.Database.World.Maps;
using NLog;
using WorldServer.World.Battlefronts;
using WorldServer.World.Battlefronts.NewDawn;

namespace WorldServer
{
    [Service(
        typeof(AnnounceService),
        typeof(BattlefrontService),
        typeof(CellSpawnService),
        typeof(ChapterService),
        typeof(CreatureService),
        typeof(DyeService),
        typeof(GameObjectService),
        typeof(GuildService),
        typeof(ItemService),
        typeof(PQuestService),
        typeof(QuestService),
        typeof(RallyPointService),
        typeof(ScenarioService),
        typeof(TokService),
        typeof(VendorService),
        typeof(WaypointService),
        typeof(XpRenownService),
        typeof(ZoneService))]
    public static class WorldMgr
    {
        public static IObjectDatabase Database;
        private static Thread _worldThread;
        private static Thread _groupThread;
        private static bool _running = true;
        public static long StartingPairing;

        public static UpperTierBattlefrontManager UpperTierBattlefrontManager;
        public static LowerTierBattlefrontManager LowerTierBattlefrontManager;
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        //Log.Success("StartingPairing: ", StartingPairing.ToString());

        #region Region

        public static List<RegionMgr> _Regions = new List<RegionMgr>();
        private static ReaderWriterLockSlim RegionsRWLock = new ReaderWriterLockSlim();
        

        public static RegionMgr GetRegion(ushort RegionId, bool Create)
        {
            RegionsRWLock.EnterReadLock();
            RegionMgr Mgr = _Regions.Find(region => region != null && region.RegionId == RegionId);
            RegionsRWLock.ExitReadLock();

            if (Mgr == null && Create)
            {
                Mgr = new RegionMgr(RegionId, ZoneService.GetZoneRegion(RegionId));
                RegionsRWLock.EnterWriteLock();
                _Regions.Add(Mgr);
                RegionsRWLock.ExitWriteLock();
            }

            return Mgr;
        }

        public static void Stop()
        {
            Log.Success("WorldMgr", "Stop");
            foreach (RegionMgr Mgr in _Regions)
                Mgr.Stop();

            ScenarioMgr.Stop();
            _running = false;

        }


        #endregion

        #region Zones

        public static Zone_Respawn GetZoneRespawn(ushort zoneId, byte realm, Player player)
        {
            Zone_Respawn respawn = null;

            if (player == null)
                return ZoneService.GetZoneRespawn(zoneId, realm);

            if (player.CurrentArea != null)
            {
                ushort respawnId = realm == 1
                    ? player.CurrentArea.OrderRespawnId
                    : player.CurrentArea.DestroRespawnId;

                #region Public Quest and Instances Respawns

                List<Zone_Respawn> resps = new List<Zone_Respawn>();

                if (player.ZoneId == 60 && player.QtsInterface.PublicQuest == null)
                {
                    resps = ZoneService.GetZoneRespawns(zoneId);
                    foreach (Zone_Respawn res in resps)
                        if (res.ZoneID == 60 && (res.RespawnID == 308 || res.RespawnID == 321))
                            return res;
                }

                if (player.QtsInterface.PublicQuest != null)
                {
                    resps = ZoneService.GetZoneRespawns(zoneId);
                    foreach (Zone_Respawn res in resps)
                        if (res.Realm == 0 && res.ZoneID == zoneId && res.RespawnID == player.QtsInterface.PublicQuest.Info.RespawnID)
                            return res;
                }

                #endregion

                if (respawnId > 0)
                {
                    Zone_Respawn resp = ZoneService.GetZoneRespawn(respawnId);

                    if (!player.CurrentArea.IsRvR)
                        return resp;

                    #region RvR area respawns
                    var front = player.Region.ndbf;

                    if (front != null)
                    {
                        var bestDist =
                            player.GetDistanceToWorldPoint(
                                ZoneService.GetWorldPosition(ZoneService.GetZone_Info((ushort)resp.ZoneID), resp.PinX, resp.PinY, resp.PinZ));

                        foreach (var keep in front.Keeps)
                        {
                            if (keep == null || keep.Zone == null || keep.Info == null)
                            {
                                Log.Error("GetZoneRespawn", "Null required Keep information");
                                continue;
                            }

                            if (keep.Realm == player.Realm &&
                                (keep.KeepStatus == KeepStatus.KEEPSTATUS_SAFE ||
                                 keep.KeepStatus == KeepStatus.KEEPSTATUS_OUTER_WALLS_UNDER_ATTACK))
                            {
                                var dist = player.GetDistanceToWorldPoint(keep.WorldPosition);
                                if (dist < bestDist)
                                {
                                    resp = new Zone_Respawn
                                    {
                                        ZoneID = keep.Zone.ZoneId,
                                        PinX = ZoneService.CalculPin(keep.Zone.Info, keep.Info.X, true),
                                        PinY = ZoneService.CalculPin(keep.Zone.Info, keep.Info.Y, false),
                                        PinZ = (ushort)keep.Info.Z
                                    };
                                    bestDist = dist;
                                }
                            }
                        }

                        return resp;
                    }

                    #endregion
                }
            }

            List<Zone_Respawn> respawns = ZoneService.GetZoneRespawns(zoneId);
            if (zoneId == 110)
                respawns = ZoneService.GetZoneRespawns(109);
            if (zoneId == 104)
                respawns = ZoneService.GetZoneRespawns(103);
            if (zoneId == 210)
                respawns = ZoneService.GetZoneRespawns(209);
            if (zoneId == 204)
                respawns = ZoneService.GetZoneRespawns(203);
            if (zoneId == 220)
                respawns = ZoneService.GetZoneRespawns(205);
            if (zoneId == 10)
                respawns = ZoneService.GetZoneRespawns(9);
            if (zoneId == 4)
                respawns = ZoneService.GetZoneRespawns(3);
            if (respawns != null)
            {
                if (player.ScnInterface.Scenario != null)
                {
                    #region Scenario Spawns

                    List<Zone_Respawn> options = new List<Zone_Respawn>();

                    foreach (Zone_Respawn res in respawns)
                    {
                        if (res.Realm != realm)
                            continue;

                        options.Add(res);
                    }

                    return options.Count == 1 ? options[0] : options[StaticRandom.Instance.Next(options.Count)];

                    #endregion
                }

                #region World Spawns

                float lastDistance = float.MaxValue;

                foreach (Zone_Respawn res in respawns)
                {
                    if (res.Realm != realm)
                        continue;

                    var pos = new Point3D(res.PinX, res.PinY, res.PinZ);
                    float distance = pos.GetDistance(player);

                    if (distance < lastDistance)
                    {
                        lastDistance = distance;
                        respawn = res;
                    }
                }

                #endregion
            }

            else
                Log.Error("WorldMgr", "Zone Respawn not found for : " + zoneId);

            return respawn;
        }

        public static List<Zone_Taxi> GetTaxis(Player Plr)
        {
            List<Zone_Taxi> L = new List<Zone_Taxi>();

            Zone_Taxi[] Taxis;
            foreach (KeyValuePair<ushort, Zone_Taxi[]> Kp in ZoneService._Zone_Taxi)
            {
                Taxis = Kp.Value;
                if (Taxis[(byte)Plr.Realm] == null || Taxis[(byte)Plr.Realm].WorldX == 0)
                    continue;

                if (Taxis[(byte)Plr.Realm].Info == null)
                    Taxis[(byte)Plr.Realm].Info = ZoneService.GetZone_Info(Taxis[(byte)Plr.Realm].ZoneID);

                if (Taxis[(byte)Plr.Realm].Info == null)
                    continue;

                if (Taxis[(byte)Plr.Realm].Enable == false)
                    continue;

                if (Taxis[(byte)Plr.Realm].Tier > 0)
                {
                    switch (Taxis[(byte)Plr.Realm].Tier)
                    {
                        case 2:
                            if (!(Plr.TokInterface.HasTok(11) || Plr.TokInterface.HasTok(44) || Plr.TokInterface.HasTok(75) || Plr.TokInterface.HasTok(140) || Plr.TokInterface.HasTok(171) || Plr.TokInterface.HasTok(107)))
                                continue;
                            break;
                        case 3:
                            if (!(Plr.TokInterface.HasTok(12) || Plr.TokInterface.HasTok(50) || Plr.TokInterface.HasTok(81) || Plr.TokInterface.HasTok(108) || Plr.TokInterface.HasTok(146) || Plr.TokInterface.HasTok(177)))
                                continue;
                            break;
                        case 4:
                            if (!(Plr.TokInterface.HasTok(18) || Plr.TokInterface.HasTok(55) || Plr.TokInterface.HasTok(86) || Plr.TokInterface.HasTok(114) || Plr.TokInterface.HasTok(182) || Plr.TokInterface.HasTok(151)))
                                continue;
                            break;
                    }
                }
                L.Add(Taxis[(byte)Plr.Realm]);
            }

            return L;
        }
        #endregion

        #region Xp / Renown

        public static uint GenerateXPCount(Player plr, Unit victim)
        {
            uint KLvl = plr.AdjustedLevel;
            uint VLvl = victim.AdjustedLevel;

            if (KLvl > VLvl + 8)
                return 0;

            uint XP = VLvl * 70;

            if (victim is Creature)
            {
                switch (victim.Rank)
                {
                    case 1:
                        XP *= 2; break;
                    case 2:
                        if (plr.WorldGroup != null)
                            XP *= 8;
                        break;
                }
            }

            if (KLvl > VLvl)
                XP -= (uint)((XP / (float)100) * (KLvl - VLvl + 1)) * 5;

            if (Program.Config.XpRate > 0)
                XP *= (uint)Program.Config.XpRate;

            return XP;
        }

        public static void GenerateXP(Player killer, Unit victim, float bonusMod)
        {
            if (killer == null) return;

            if (killer.Level != killer.EffectiveLevel)
                bonusMod = 0.0f;

            if (killer.PriorityGroup == null)
                killer.AddXp((uint)(GenerateXPCount(killer, victim) * bonusMod), true, true);
            else
                killer.PriorityGroup.AddXpFromKill(killer, victim, bonusMod);
        }
        
        public static uint GenerateRenownCount(Player killer, Player victim)
        {
            if (killer == null || victim == null || killer == victim)
                return 0;

            uint renownPoints = (uint)(7.31f * (victim.AdjustedRenown + victim.AdjustedLevel));

            if (killer.AdjustedLevel > 15 && killer.AdjustedLevel < 31)
                renownPoints = (uint)(renownPoints * 1.5f);

            return renownPoints;
        }

        #endregion
        
        #region Vendors
        public static void SendVendor(Player Plr, ushort id)
        {
            if (Plr == null)
                return;

            //guildrank check
            List<Vendor_items> Itemsprecheck = VendorService.GetVendorItems(id).ToList();
            List<Vendor_items> Items = new List<Vendor_items>();

            foreach(Vendor_items vi in Itemsprecheck)
            {
                if (vi.ReqGuildlvl > 0 && Plr.GldInterface.IsInGuild() && vi.ReqGuildlvl > Plr.GldInterface.Guild.Info.Level)
                    continue;
                Items.Add(vi);
            }


            byte Page = 0;
            int Count = Items.Count;
            while (Count > 0)
            {
                byte ToSend = (byte)Math.Min(Count, VendorService.MAX_ITEM_PAGE);
                if (ToSend <= Count)
                    Count -= ToSend;
                else
                    Count = 0;

                SendVendorPage(Plr, ref Items, ToSend, Page);

                ++Page;
            }

            Plr.ItmInterface.SendBuyBack();
        }
        public static void SendVendorPage(Player Plr, ref List<Vendor_items> Vendors, byte Count, byte Page)
        {
            Count = (byte)Math.Min(Count, Vendors.Count);

            PacketOut Out = new PacketOut((byte)Opcodes.F_INIT_STORE, 256);
            Out.WriteByte(3);
            Out.WriteByte(0);
            Out.WriteByte(Page);
            Out.WriteByte(Count);
            Out.WriteByte((byte)(Page > 0 ? 0 : 1));
            Out.WriteByte(1);
            Out.WriteByte(0);

            if (Page == 0)
                Out.WriteByte(0);

            for (byte i = 0; i < Count; ++i)
            {
                Out.WriteByte(i);
                Out.WriteByte(1);
                Out.WriteUInt32(Vendors[i].Price);
                Item.BuildItem(ref Out, null, Vendors[i].Info, null, 0, 1);

                if (Plr != null && Plr.ItmInterface != null && Vendors[i].Info != null && Vendors[i].Info.ItemSet != 0)
                    Plr.ItmInterface.SendItemSetInfoToPlayer(Plr, Vendors[i].Info.ItemSet);

                if ((byte)Vendors[i].ItemsReq.Count > 0)
                {
                    Out.WriteByte(1);
                    foreach (KeyValuePair<uint, ushort> Kp in Vendors[i].ItemsReq)
                    {
                        Item_Info item = ItemService.GetItem_Info(Kp.Key);
                        Out.WriteUInt32(Kp.Key);
                        Out.WriteUInt16((ushort)item.ModelId);
                        Out.WritePascalString(item.Name);
                        Out.WriteUInt16(Kp.Value);
                    }

                }
                if ((byte)Vendors[i].ItemsReq.Count == 1)
                    Out.Fill(0, 18);
                else if ((byte)Vendors[i].ItemsReq.Count == 2)
                    Out.Fill(0, 9);
                else
                    Out.Fill(0, 1);

            }

            Out.WriteByte(0);
            Plr.SendPacket(Out);

            Vendors.RemoveRange(0, Count);
        }

        public static void BuyItemVendor(Player Plr, InteractMenu Menu, ushort id)
        {
            int Num = (Menu.Page * VendorService.MAX_ITEM_PAGE) + Menu.Num;
            ushort Count = Menu.Packet.GetUint16();
            if (Count == 0)
                Count = 1;

            //guildrank check
            List<Vendor_items> Itemsprecheck = VendorService.GetVendorItems(id).ToList();
            List<Vendor_items> Vendors = new List<Vendor_items>();

            foreach (Vendor_items vi in Itemsprecheck)
            {
                if (vi.ReqGuildlvl > 0 && Plr.GldInterface.IsInGuild() && vi.ReqGuildlvl > Plr.GldInterface.Guild.Info.Level)
                    continue;
                Vendors.Add(vi);
            }

            if (Vendors.Count <= Num)
                return;

            if (!Plr.HasMoney((Vendors[Num].Price) * Count))
            {
                Plr.SendLocalizeString("", ChatLogFilters.CHATLOGFILTERS_USER_ERROR, Localized_text.TEXT_MERCHANT_INSUFFICIENT_MONEY_TO_BUY);
                return;
            }

            foreach (KeyValuePair<uint,ushort> Kp in Vendors[Num].ItemsReq)
            {
                if (!Plr.ItmInterface.HasItemCountInInventory(Kp.Key, (ushort)(Kp.Value * Count)))
                {
                    Plr.SendLocalizeString("", ChatLogFilters.CHATLOGFILTERS_USER_ERROR, Localized_text.TEXT_MERCHANT_FAIL_PURCHASE_REQUIREMENT);
                    return;
                }
            }

            if (Vendors[Num].ReqTokUnlock > 0 && !Plr.TokInterface.HasTok(Vendors[Num].ReqTokUnlock))
                return;

            ItemResult result = Plr.ItmInterface.CreateItem(Vendors[Num].Info, Count);
            if (result == ItemResult.RESULT_OK)
            {
                Plr.RemoveMoney(Vendors[Num].Price * Count);
                foreach (KeyValuePair<uint,ushort> Kp in Vendors[Num].ItemsReq)
                    Plr.ItmInterface.RemoveItems(Kp.Key, (ushort)(Kp.Value * Count));
            }
            else if (result == ItemResult.RESULT_MAX_BAG)
            {
                Plr.SendLocalizeString("", ChatLogFilters.CHATLOGFILTERS_USER_ERROR, Localized_text.TEXT_MERCHANT_INSUFFICIENT_SPACE_TO_BUY);
            }
            else if (result == ItemResult.RESULT_ITEMID_INVALID)
            {

            }
        }
        #endregion
        
        #region Quests
        // TODO move that to QuestService
        public static void GenerateObjective(Quest_Objectives Obj, Quest Q)
        {
            switch ((Objective_Type)Obj.ObjType)
            {
                case Objective_Type.QUEST_KILL_PLAYERS:
                    {
                        if (Obj.Description.Length < 1)
                            Obj.Description = "Enemy Players";
                    }
                    break;

                case Objective_Type.QUEST_SPEAK_TO:
                    {
                        uint ObjID = 0;
                        uint.TryParse(Obj.ObjID, out ObjID);

                        if (ObjID != 0)
                            Obj.Creature = CreatureService.GetCreatureProto(ObjID);

                        if (Obj.Creature == null)
                        {
                            Obj.Description = "Invalid NPC - " + Obj.Entry + ",ObjId=" + Obj.ObjID;
                        }
                        else
                        {
                            if (Obj.Description == null || Obj.Description.Length <= Obj.Creature.Name.Length)
                                Obj.Description = "Speak to " + Obj.Creature.Name;
                        }

                    }
                    break;

                case Objective_Type.QUEST_USE_GO:
                    {
                        uint ObjID = 0;
                        uint.TryParse(Obj.ObjID, out ObjID);

                        if (ObjID != 0)
                            Obj.GameObject = GameObjectService.GetGameObjectProto(ObjID);

                        if (Obj.GameObject == null)
                        {
                            Obj.Description = "Invalid GameObject - QuestID " + Obj.Entry + ",ObjId=" + Obj.ObjID;
                        }
                        else
                        {
                            if (Obj.Description == null || Obj.Description.Length <= Obj.GameObject.Name.Length)
                                Obj.Description = "Find " + Obj.GameObject.Name;
                        }

                    }
                    break;

                case Objective_Type.QUEST_KILL_MOB:
                    {
                        uint ObjID = 0;
                        uint.TryParse(Obj.ObjID, out ObjID);

                        if (ObjID != 0)
                            Obj.Creature = CreatureService.GetCreatureProto(ObjID);

                        if (Obj.Creature == null)
                        {
                            Obj.Description = "Invalid Creature - QuestID " + Obj.Entry + ",ObjId=" + Obj.ObjID;
                        }
                        else
                        {
                            if (Obj.Description == null || Obj.Description.Length <= Obj.Creature.Name.Length)
                                Obj.Description = "Kill " + Obj.Creature.Name;
                        }

                    }
                    break;

                case Objective_Type.QUEST_KILL_GO:
                    {
                        uint ObjID = 0;
                        uint.TryParse(Obj.ObjID, out ObjID);

                        if (ObjID != 0)
                            Obj.GameObject = GameObjectService.GetGameObjectProto(ObjID);

                        if (Obj.GameObject == null)
                        {
                            Obj.Description = "Invalid GameObject - QuestID " + Obj.Entry + ",ObjId=" + Obj.ObjID;
                        }
                        else
                        {
                            if (Obj.Description == null || Obj.Description.Length <= Obj.GameObject.Name.Length)
                                Obj.Description = "Destroy " + Obj.GameObject.Name;
                        }

                    }
                    break;

                case Objective_Type.QUEST_USE_ITEM:
                case Objective_Type.QUEST_GET_ITEM:
                    {
                        uint ObjID = 0;
                        uint.TryParse(Obj.ObjID, out ObjID);

                        if (ObjID != 0)
                        {
                            Obj.Item = ItemService.GetItem_Info(ObjID);
                            if (Obj.Item == null)
                            {
                                int a = Obj.Quest.Particular.IndexOf("kill the ", StringComparison.OrdinalIgnoreCase);
                                if (a >= 0)
                                {
                                    string[] RestWords = Obj.Quest.Particular.Substring(a + 9).Split(' ');
                                    string Name = RestWords[0] + " " + RestWords[1];
                                    Creature_proto Proto = CreatureService.GetCreatureProtoByName(Name) ?? CreatureService.GetCreatureProtoByName(RestWords[0]);
                                    if (Proto != null)
                                    {
                                        Obj.Item = new Item_Info();
                                        Obj.Item.Entry = ObjID;
                                        Obj.Item.Name = Obj.Description;
                                        Obj.Item.MaxStack = 20;
                                        Obj.Item.ModelId = 531;
                                        ItemService._Item_Info.Add(Obj.Item.Entry, Obj.Item);

                                        Log.Info("WorldMgr", "Creating Quest(" + Obj.Entry + ") Item : " + Obj.Item.Entry + ",  " + Obj.Item.Name + "| Adding Loot to : " + Proto.Name);
                                        /*Creature_loot loot = new Creature_loot();
                                        loot.Entry = Proto.Entry;
                                        loot.ItemId = Obj.Item.Entry;
                                        loot.Info = Obj.Item;
                                        loot.Pct = 0;
                                        GetCreatureSpecificLootFor(Proto.Entry).Add(loot);*/
                                    }
                                }
                            }
                        }
                    }
                    break;

                case Objective_Type.QUEST_WIN_SCENARIO:
                    {
                        ushort ObjID = 0;
                        ushort.TryParse(Obj.ObjID, out ObjID);

                        if (ObjID != 0)
                            Obj.Scenario = ScenarioService.GetScenario_Info(ObjID);

                        if (Obj.Scenario == null)
                            Obj.Description = "Invalid Scenario - QuestID=" + Obj.Entry + ", ObjId=" + Obj.ObjID;
                        else
                            if (Obj.Description == null || Obj.Description.Length <= Obj.Scenario.Name.Length)
                            Obj.Description = "Win " + Obj.Scenario.Name;
                    }
                    break;

                case Objective_Type.QUEST_CAPTURE_BO:
                    {
                        ushort ObjID = 0;
                        ushort.TryParse(Obj.ObjID, out ObjID);

                        if (ObjID != 0)
                        {
                            foreach (List<Battlefront_Objective> boList in BattlefrontService._BattlefrontObjectives.Values)
                            {
                                foreach (Battlefront_Objective bo in boList)
                                {
                                    if (bo.Entry == ObjID)
                                    {
                                        Obj.BattlefrontObjective = bo;
                                        break;
                                    }
                                }

                                if (Obj.BattlefrontObjective != null)
                                    break;
                            }
                        }

                        if (Obj.BattlefrontObjective == null)
                            Obj.Description = "Invalid Battlefield Objective - QuestID=" + Obj.Entry + ", ObjId=" + Obj.ObjID;
                        else
                            if (Obj.Description == null || Obj.Description.Length <= Obj.BattlefrontObjective.Name.Length)
                            Obj.Description = "Capture " + Obj.Scenario.Name;
                    }
                    break;

                case Objective_Type.QUEST_CAPTURE_KEEP:
                    {
                        ushort ObjID = 0;
                        ushort.TryParse(Obj.ObjID, out ObjID);

                        if (ObjID != 0)
                        {
                            foreach (List<Keep_Info> keepList in BattlefrontService._KeepInfos.Values)
                            {
                                foreach (Keep_Info keep in keepList)
                                {
                                    if (keep.KeepId == ObjID)
                                    {
                                        Obj.Keep = keep;
                                        break;
                                    }
                                }

                                if (Obj.Keep != null)
                                    break;
                            }
                        }

                        if (Obj.Keep == null)
                            Obj.Description = "Invalid Keep - QuestID=" + Obj.Entry + ", ObjId=" + Obj.ObjID;
                        else
                            if (Obj.Description == null || Obj.Description.Length <= Obj.Keep.Name.Length)
                            Obj.Description = "Capture " + Obj.Keep.Name;
                    }
                    break;
            }
        }
        #endregion

        #region Relation

        [LoadingFunction(false)]
        public static void LoadRelation()
        {
            Log.Success("LoadRelation", "Loading Relations");

            foreach (Item_Info info in ItemService._Item_Info.Values)
            {
                if (info.Career != 0)
                {
                    foreach (KeyValuePair<byte, CharacterInfo> Kp in CharMgr.CharacterInfos)
                    {
                        if ((info.Career & (1 << (Kp.Value.CareerLine - 1))) == 0)
                            continue;

                        info.Realm = Kp.Value.Realm;
                        break;

                    }
                }

                else if (info.Race > 0)
                {
                    if (((Constants.RaceMaskDwarf + Constants.RaceMaskHighElf + Constants.RaceMaskEmpire) & info.Race) > 0)
                        info.Realm = 1;
                    else info.Realm = 2;
                }
            }

            LoadChapters();
            LoadPublicQuests();
            LoadQuestsRelation();
            LoadScripts(false);

            foreach (List<Keep_Info> keepInfos in BattlefrontService._KeepInfos.Values)
                foreach (Keep_Info keepInfo in keepInfos)
                    if (PQuestService._PQuests.ContainsKey(keepInfo.PQuestId))
                        keepInfo.PQuest = PQuestService._PQuests[keepInfo.PQuestId];

            CharMgr.Database.ExecuteNonQuery("UPDATE war_characters.characters_value SET Online=0;");

            // Preload T4 regions
            Log.Info("Regions", "Preloading pairing regions...");
            // Tier 1
            GetRegion(1, true); // dw/gs
            GetRegion(3, true); // he/de
            GetRegion(8, true); // em/ch

            // Tier 2
            GetRegion(12, true); // dw/gs
            GetRegion(15, true); // he/de
            GetRegion(14, true); // em/ch

            // Tier 3
            GetRegion(10, true); // dw/gs
            GetRegion(16, true); // he/de
            GetRegion(6, true); // em/ch

            // Tier 4
            GetRegion(2, true); // dw/gs
            GetRegion(4, true);  // he/de
            GetRegion(11, true); // em/ch

            GetRegion(9, true); // lotd
            Log.Success("Regions", "Preloaded pairing regions.");
        }

        public static void LoadChapters()
        {
            Log.Success("LoadChapters", "Loading Zone from Chapters");

            long InvalidChapters = 0;

            Zone_Info Zone = null;
            Chapter_Info Info;
            foreach (KeyValuePair<uint, Chapter_Info> Kp in ChapterService._Chapters)
            {
                Info = Kp.Value;
                Zone = ZoneService.GetZone_Info(Info.ZoneId);

                if (Zone == null || (Info.PinX <= 0 && Info.PinY <= 0))
                {
                    Log.Debug("LoadChapters", "Chapter (" + Info.Entry + ")[" + Info.Name + "] Invalid");
                    ++InvalidChapters;
                }

                if (Info.T1Rewards == null)
                    Info.T1Rewards = new List<Chapter_Reward>();
                if (Info.T2Rewards == null)
                    Info.T2Rewards = new List<Chapter_Reward>();
                if (Info.T3Rewards == null)
                    Info.T3Rewards = new List<Chapter_Reward>();

                List<Chapter_Reward> Rewards;
                if (ChapterService._Chapters_Reward.TryGetValue(Info.Entry, out Rewards))
                {
                    foreach (Chapter_Reward CW in Rewards)
                    {
                        if (Info.Tier1InfluenceCount == CW.InfluenceCount)
                        {
                            Info.T1Rewards.Add(CW);
                        }
                        else if (Info.Tier2InfluenceCount == CW.InfluenceCount)
                        {
                            Info.T2Rewards.Add(CW);
                        }
                        else if (Info.Tier3InfluenceCount == CW.InfluenceCount)
                        {
                            Info.T3Rewards.Add(CW);
                        }
                    }
                }


                foreach (Chapter_Reward Reward in Info.T1Rewards.ToArray())
                {
                    Reward.Item = ItemService.GetItem_Info(Reward.ItemId);
                    Reward.Chapter = Info;

                    if (Reward.Item == null)
                        Info.T1Rewards.Remove(Reward);
                }

                foreach (Chapter_Reward Reward in Info.T2Rewards.ToArray())
                {
                    Reward.Item = ItemService.GetItem_Info(Reward.ItemId);
                    Reward.Chapter = Info;

                    if (Reward.Item == null)
                        Info.T2Rewards.Remove(Reward);
                }
                foreach (Chapter_Reward Reward in Info.T3Rewards.ToArray())
                {
                    Reward.Item = ItemService.GetItem_Info(Reward.ItemId);
                    Reward.Chapter = Info;

                    if (Reward.Item == null)
                        Info.T3Rewards.Remove(Reward);
                }

                CellSpawnService.GetRegionCell(Zone.Region, (ushort)((float)(Info.PinX / 4096) + Zone.OffX), (ushort)((float)(Info.PinY / 4096) + Zone.OffY)).AddChapter(Info);
            }



            if (InvalidChapters > 0)
                Log.Error("LoadChapters", "[" + InvalidChapters + "] Invalid Chapter(s)");
        }
        public static void LoadPublicQuests()
        {
            Zone_Info Zone = null;
            PQuest_Info Info;
            List<string> skippedPQs = new List<string>();

            foreach (KeyValuePair<uint, PQuest_Info> Kp in PQuestService._PQuests)
            {
                Info = Kp.Value;
                Zone = ZoneService.GetZone_Info(Info.ZoneId);
                if (Zone == null)
                    continue;


                if (!PQuestService._PQuest_Objectives.TryGetValue(Info.Entry, out Info.Objectives))
                    Info.Objectives = new List<PQuest_Objective>();
                else
                {
                    foreach (PQuest_Objective Obj in Info.Objectives)
                    {
                        Obj.Quest = Info;
                        PQuestService.GeneratePQuestObjective(Obj, Obj.Quest);

                        if (!PQuestService._PQuest_Spawns.TryGetValue(Obj.Guid, out Obj.Spawns))
                            Obj.Spawns = new List<PQuest_Spawn>();
                    }
                }

                //Log.Info("LoadPublicQuests", "Loaded public quest "+Info.Entry+" to region "+Zone.Region+" cell at X: "+ ((float)(Info.PinX / 4096) + Zone.OffX)+" "+ (float)(Info.PinY / 4096) + Zone.OffY);

                bool skipLoad = false;

                foreach (List<Keep_Info> keepInfos in BattlefrontService._KeepInfos.Values)
                {
                    if (keepInfos.Any(keep => keep.PQuestId == Kp.Key))
                    {
                        skippedPQs.Add(Kp.Value.Name);
                        skipLoad = true;
                        break;
                    }
                }

                if (!skipLoad)
                   CellSpawnService.GetRegionCell(Zone.Region, (ushort)((float)(Info.PinX / 4096) + Zone.OffX), (ushort)((float)(Info.PinY / 4096) + Zone.OffY)).AddPQuest(Info);
            }

            if (skippedPQs.Count > 0)
                Log.Info("Skipped PQs", string.Join(", ", skippedPQs));
        }
        public static void LoadQuestsRelation()
        {
            QuestService.LoadQuestCreatureStarter();
            QuestService.LoadQuestCreatureFinisher();

            foreach (KeyValuePair<uint, Creature_proto> Kp in CreatureService.CreatureProtos)
            {
                Kp.Value.StartingQuests = QuestService.GetStartQuests(Kp.Key);
                Kp.Value.FinishingQuests = QuestService.GetFinishersQuests(Kp.Key);
            }

            Quest quest;

            int MaxGuid = 0;
            foreach (KeyValuePair<int, Quest_Objectives> Kp in QuestService._Objectives)
            {
                if (Kp.Value.Guid >= MaxGuid)
                    MaxGuid = Kp.Value.Guid;
            }

            foreach (KeyValuePair<int, Quest_Objectives> Kp in QuestService._Objectives)
            {
                quest = Kp.Value.Quest = QuestService.GetQuest(Kp.Value.Entry);
                if (quest == null)
                    continue;

                quest.Objectives.Add(Kp.Value);
            }

            foreach (Quest_Map Q in QuestService._QuestMaps)
            {
                quest = QuestService.GetQuest(Q.Entry);
                if (quest == null)
                    continue;

                quest.Maps.Add(Q);
            }

            foreach (KeyValuePair<ushort, Quest> Kp in QuestService._Quests)
            {
                quest = Kp.Value;

                if (quest.Objectives.Count == 0)
                {
                    uint Finisher = QuestService.GetQuestCreatureFinisher(quest.Entry);
                    if (Finisher != 0)
                    {
                        Quest_Objectives NewObj = new Quest_Objectives();
                        NewObj.Guid = ++MaxGuid;
                        NewObj.Entry = quest.Entry;
                        NewObj.ObjType = (uint)Objective_Type.QUEST_SPEAK_TO;
                        NewObj.ObjID = Finisher.ToString();
                        NewObj.ObjCount = 1;
                        NewObj.Quest = quest;

                        quest.Objectives.Add(NewObj);
                        QuestService._Objectives.Add(NewObj.Guid, NewObj);

                        Log.Debug("WorldMgr", "Creating Objective for quest with no objectives: " + Kp.Value.Entry + " " + Kp.Value.Name);
                    }
                }
            }

            foreach (KeyValuePair<int, Quest_Objectives> Kp in QuestService._Objectives)
            {
                if (Kp.Value.Quest == null)
                    continue;
                GenerateObjective(Kp.Value, Kp.Value.Quest);
            }

            string sItemID, sCount;
            uint ItemID, Count;
            Item_Info Info;
            foreach (KeyValuePair<ushort, Quest> Kp in QuestService._Quests)
            {
                if (Kp.Value.Choice.Length <= 0)
                    continue;

                // [5154,12],[128,1]
                string[] Rewards = Kp.Value.Choice.Split('[');
                foreach (string Reward in Rewards)
                {
                    if (Reward.Length <= 0)
                        continue;

                    sItemID = Reward.Substring(0, Reward.IndexOf(','));
                    sCount = Reward.Substring(sItemID.Length + 1, Reward.IndexOf(']') - sItemID.Length - 1);

                    ItemID = uint.Parse(sItemID);
                    Count = uint.Parse(sCount);

                    Info = ItemService.GetItem_Info(ItemID);
                    if (Info == null)
                        continue;

                    if (!Kp.Value.Rewards.ContainsKey(Info))
                        Kp.Value.Rewards.Add(Info, Count);
                    else
                        Kp.Value.Rewards[Info] += Count;
                }
            }
        }
        

        #endregion

        #region Scripts

        public static CSharpScriptCompiler ScriptCompiler;
        public static Dictionary<string, Type> LocalScripts = new Dictionary<string, Type>();
        public static Dictionary<string, AGeneralScript> GlobalScripts = new Dictionary<string, AGeneralScript>();
        public static Dictionary<uint, Type> CreatureScripts = new Dictionary<uint, Type>();
        public static Dictionary<uint, Type> GameObjectScripts = new Dictionary<uint, Type>();
        public static ScriptsInterface GeneralScripts;

        public static void LoadScripts(bool Reload)
        {
            GeneralScripts = new ScriptsInterface();

            ScriptCompiler = new CSharpScriptCompiler();
            ScriptCompiler.LoadScripts();
            GeneralScripts.ClearScripts();

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes())
                {
                    if (type.IsClass != true)
                        continue;

                    if (!type.IsSubclassOf(typeof(AGeneralScript)))
                        continue;

                    foreach (GeneralScriptAttribute at in type.GetCustomAttributes(typeof(GeneralScriptAttribute), true))
                    {
                        if (!string.IsNullOrEmpty(at.ScriptName))
                            at.ScriptName = at.ScriptName.ToLower();

                        Log.Success("Scripting", "Registering Script :" + at.ScriptName);

                        if (at.GlobalScript)
                        {
                            AGeneralScript Script = Activator.CreateInstance(type) as AGeneralScript;
                            Script.ScriptName = at.ScriptName;
                            GeneralScripts.RemoveScript(Script.ScriptName);
                            GeneralScripts.AddScript(Script);
                            GlobalScripts[at.ScriptName] = Script;
                        }
                        else
                        {
                            if (at.CreatureEntry != 0)
                            {
                                Log.Success("Scripts", "Registering Creature Script :" + at.CreatureEntry);

                                if (!CreatureScripts.ContainsKey(at.CreatureEntry))
                                {
                                    CreatureScripts[at.CreatureEntry] = type;
                                }
                                else
                                {
                                    CreatureScripts[at.CreatureEntry] = type;
                                }
                            }
                            else if (at.GameObjectEntry != 0)
                            {
                                Log.Success("Scripts", "Registering GameObject Script :" + at.GameObjectEntry);

                                if (!GameObjectScripts.ContainsKey(at.GameObjectEntry))
                                {
                                    GameObjectScripts[at.GameObjectEntry] = type;
                                }
                                else
                                {
                                    GameObjectScripts[at.GameObjectEntry] = type;
                                }
                            }
                            else if (!string.IsNullOrEmpty(at.ScriptName))
                            {
                                Log.Success("Scripts", "Registering Name Script :" + at.ScriptName);

                                if (!LocalScripts.ContainsKey(at.ScriptName))
                                {
                                    LocalScripts[at.ScriptName] = type;
                                }
                                else
                                {
                                    LocalScripts[at.ScriptName] = type;
                                }
                            }
                        }
                    }
                }
            }

            Log.Success("Scripting", "Loaded  : " + (GeneralScripts.Scripts.Count + LocalScripts.Count) + " Scripts");

            if (Reload)
            {
                if (Program.Server != null)
                    Program.Server.LoadPacketHandler();
            }
        }

        public static AGeneralScript GetScript(Object Obj, string ScriptName)
        {
            if (!string.IsNullOrEmpty(ScriptName))
            {
                ScriptName = ScriptName.ToLower();

                if (GlobalScripts.ContainsKey(ScriptName))
                    return GlobalScripts[ScriptName];
                if (LocalScripts.ContainsKey(ScriptName))
                {
                    AGeneralScript Script = Activator.CreateInstance(LocalScripts[ScriptName]) as AGeneralScript;
                    Script.ScriptName = ScriptName;
                    return Script;
                }
            }
            else
            {
                if (Obj.IsCreature() && CreatureScripts.ContainsKey(Obj.GetCreature().Spawn.Entry))
                {
                    AGeneralScript Script = Activator.CreateInstance(CreatureScripts[Obj.GetCreature().Spawn.Entry]) as AGeneralScript;
                    Script.ScriptName = Obj.GetCreature().Spawn.Entry.ToString();
                    return Script;
                }

                if (Obj.IsGameObject() && GameObjectScripts.ContainsKey(Obj.GetGameObject().Spawn.Entry))
                {
                    AGeneralScript Script = Activator.CreateInstance(GameObjectScripts[Obj.GetGameObject().Spawn.Entry]) as AGeneralScript;
                    Script.ScriptName = Obj.GetGameObject().Spawn.Entry.ToString();
                    return Script;
                }
            }

            return null;
        }

        public static void UpdateScripts(long Tick)
        {
            GeneralScripts.Update(Tick);
        }

        #endregion

        #region Scenarios
        public static ScenarioMgr ScenarioMgr;

        public static InstanceMgr InstanceMgr;

        [LoadingFunction(true)]
        public static void StartScenarioMgr()
        {
            ScenarioMgr = new ScenarioMgr(ScenarioService.ActiveScenarios);
        }

        [LoadingFunction(true)]
        public static void StartInstanceMgr()
        {
            InstanceMgr = new InstanceMgr();
        }

        #endregion

        #region Settings

        public static WorldSettingsMgr WorldSettingsMgr;

        [LoadingFunction(true)]
        public static void StartWorldSettingsMgr()
        {
            WorldSettingsMgr = new WorldSettingsMgr();
        }

        #endregion



        #region Campaign

        public static void WorldUpdateStart()
        {
            Log.Debug("WorldMgr", "Starting World Monitor...");

            _worldThread = new Thread(WorldUpdate);
            _worldThread.Start();
        }

        public static void GroupUpdateStart()
        {
            Log.Debug("WorldMgr", "Starting Group Updater...");

            _groupThread = new Thread(GroupUpdate);
            _groupThread.Start();
        }


        public static Dictionary<int,int> GetZonesFightLevel()
        {
            var level = new Dictionary<int,int>();
            foreach (var region in WorldMgr._Regions.Where(e => e.Bttlfront != null).ToList())
            {
                foreach (var zone in region.ZonesMgr.ToList())
                {
                    var hotspots = zone.GetHotSpots();
                    if (hotspots.Count > 0)
                        level[zone.ZoneId] = hotspots.Where(e=>e.Item2 >= ZoneMgr.LOW_FIGHT).Max(e => e.Item2);
                }
            }
            return level;
        }

        /// <summary>
        /// Show swords on world map if zone has people fighting it
        /// </summary>
        public static void SendZoneFightLevel(Player player = null)
        {
            var fightLevel = GetZonesFightLevel();

            PacketOut Out = new PacketOut((byte)Opcodes.F_UPDATE_HOT_SPOT);
            Out.WriteByte((byte)fightLevel.Count);
            Out.WriteByte(2); //world hotspots
            Out.WriteByte(0);

            //fight level
            uint none = 0x00000000;
            uint small = 0x01000000;
            uint large = 0x01020000;
            uint huge = 0x01020100;

            foreach (var zoneId in fightLevel.Keys)
            {
                Out.WriteByte((byte)zoneId);

                if (fightLevel[zoneId] >= ZoneMgr.LARGE_FIGHT)
                    Out.WriteUInt32(huge);
                else if (fightLevel[zoneId] > ZoneMgr.MEDIUM_FIGHT)
                    Out.WriteUInt32(large);
                else if (fightLevel[zoneId] > ZoneMgr.LOW_FIGHT)
                    Out.WriteUInt32(small);
                else
                    Out.WriteUInt32(none);
            }

            if (player != null)
                player.SendPacket(Out);
            else
            {
                lock (Player._Players)
                {
                    foreach (Player pPlr in Player._Players)
                    {
                        if (pPlr == null || pPlr.IsDisposed || !pPlr.IsInWorld())
                            continue;

                        pPlr.SendCopy(Out);
                    }
                }
            }

            foreach (var region in WorldMgr._Regions.Where(e => e.Bttlfront != null).ToList())
            {
                foreach (var zone in region.ZonesMgr.ToList())
                {
                    zone.SendHotSpots(player);
                }
            }
        }


        private static void WorldUpdate()
        {
            while (_running)
            {
                if (ZoneService._Zone_Info != null)
                {
                    SendZoneFightLevel();

                    foreach (var region in WorldMgr._Regions.Where(e => e.Bttlfront != null).ToList())
                    {
                        foreach (var zone in region.ZonesMgr.ToList())
                        {
                            zone.DecayHotspots();
                        }
                    }
                }
                Thread.Sleep(15000);
            }

        }


        private static void GroupUpdate()
        {
            while (_running)
            {
                List<Group> _groups = new List<Group>();
                lock (Group.WorldGroups)
                {
                    foreach (Group g in Group.WorldGroups)
                    {
                        try
                        {
                            _groups.Add(g);
                        }
                        catch
                        {
                            continue;
                        }
                    }
                }
                List<KeyValuePair<uint, GroupAction>> _worldActions = new List<KeyValuePair<uint, GroupAction>>();
                lock (Group._pendingGroupActions)
                {
                    foreach (KeyValuePair<uint, GroupAction> kp in Group._pendingGroupActions)
                    {
                        _worldActions.Add(kp);
                    }
                    Group._pendingGroupActions.Clear();
                }

                foreach (Group g in _groups)
                {
                    try
                    {
                        foreach (KeyValuePair<uint, GroupAction> grpAction in _worldActions)
                        {
                            if(g.GroupId == grpAction.Key)
                                g.EnqueuePendingGroupAction(grpAction.Value);
                        }

                        g.Update(TCPManager.GetTimeStampMS());
                    }
                    catch(Exception e)
                    {
                        Log.Error("Caught exception", "Exception thrown: "+e);
                        continue;
                    }
                }

                _worldActions.Clear();
                _groups.Clear();

                Thread.Sleep(100);
            }

        }

        public static void BuildCaptureStatus(PacketOut Out, RegionMgr region)
        {
            if (region == null)
                Out.Fill(0, 3);
            else
                if (region.ndbf != null)
                    region.ndbf.WriteCaptureStatus(Out);
                else
                {
                    region.ndbf.WriteCaptureStatus(Out, region.ndbf.LockingRealm);    
            }
            
        }

        public static void BuildBattlefrontStatus(PacketOut Out, RegionMgr region)
        {
            if (region == null)
                Out.Fill(0, 3);
            else if (region.ndbf != null)
            {
                region.ndbf.WriteBattlefrontStatus(Out);
            }
            else
            {
                region.ndbf.WriteBattlefrontStatus(Out);
            }
        }

        public static void SendCampaignStatus(Player plr)
        {
            _logger.Trace("Send Campaign Status");
            PacketOut Out = new PacketOut((byte) Opcodes.F_CAMPAIGN_STATUS, 159);
            Out.WriteHexStringBytes("0005006700CB00"); // 7

            // Dwarfs vs Greenskins T1
            BuildCaptureStatus(Out, GetRegion(1, false));

            // Dwarfs vs Greenskins T2
            BuildCaptureStatus(Out, GetRegion(12, false));

            // Dwarfs vs Greenskins T3
            BuildCaptureStatus(Out, GetRegion(10, false));

            // Dwarfs vs Greenskins T4
            BuildCaptureStatus(Out, GetRegion(2, false));

            // Empire vs Chaos T1
            BuildCaptureStatus(Out, GetRegion(8, false));

            // Empire vs Chaos T2
            BuildCaptureStatus(Out, GetRegion(14, false));

            // Empire vs Chaos T3
            BuildCaptureStatus(Out, GetRegion(6, false));

            // Empire vs Chaos T4
            BuildCaptureStatus(Out, GetRegion(11, false));

            // High Elves vs Dark Elves T1
            BuildCaptureStatus(Out, GetRegion(3, false));

            // High Elves vs Dark Elves T2
            BuildCaptureStatus(Out, GetRegion(15, false));

            // High Elves vs Dark Elves T3
            BuildCaptureStatus(Out, GetRegion(16, false));

            // High Elves vs Dark Elves T4
            BuildCaptureStatus(Out, GetRegion(4, false));

            Out.Fill(0,83);

            // RB   4/24/2016   Added logic for T4 campaign progression.
            //gs t4
            // 0 contested 1 order controled 2 destro controled 3 notcontroled locked
            Out.WriteByte(3);   //dwarf fort
            BuildBattlefrontStatus(Out, GetRegion(2, false));   //kadrin valley
            Out.WriteByte(3);   //orc fort

            //chaos t4
            Out.WriteByte(3);   //empire fort
            BuildBattlefrontStatus(Out, GetRegion(11, false));   //reikland
            Out.WriteByte(3);   //chaos fort

            //elf
            Out.WriteByte(3);   //elf fort
            BuildBattlefrontStatus(Out, GetRegion(4, false));   //etaine
            Out.WriteByte(3);   //delf fort

            Out.WriteByte(0); // Order underdog rating
            Out.WriteByte(0); // Destruction underdog rating

            if (plr == null)
            {
                byte[] buffer = Out.ToArray();

                lock (Player._Players)
                {
                    foreach (Player player in Player._Players)
                    {
                        if (player == null || player.IsDisposed || !player.IsInWorld())
                            continue;

                        PacketOut playerCampaignStatus = new PacketOut(0, 159) {Position = 0};
                        playerCampaignStatus.Write(buffer, 0, buffer.Length);

                        if (player.Region?.ndbf != null)
                            player.Region.ndbf.WriteVictoryPoints(player.Realm, playerCampaignStatus);

                        else
                            playerCampaignStatus.Fill(0, 9);

                        playerCampaignStatus.Fill(0, 4);

                        player.SendPacket(playerCampaignStatus);
                    }
                }
            }
            else
            {
                if (plr.Region?.ndbf != null)
                    plr.Region.ndbf.WriteVictoryPoints(plr.Realm, Out);

                else
                    Out.Fill(0, 9);

                Out.Fill(0, 4);

                plr.SendPacket(Out);
            }
        }

        // This is used to change the fronts during campaign, DoomsDay changes below
        public static void EvaluateT4CampaignStatus(ushort Region)
        {
            ProximityProgressingBattlefront DvG = (ProximityProgressingBattlefront)GetRegion(2, false).Bttlfront;
            ProximityProgressingBattlefront EvC = (ProximityProgressingBattlefront)GetRegion(11, false).Bttlfront; 
            ProximityProgressingBattlefront HEvDE = (ProximityProgressingBattlefront)GetRegion(4, false).Bttlfront;
            // codeword p0tat0 - changed for DoomsDay
            /*ProgressingBattlefront DvG = (ProgressingBattlefront)GetRegion(2, false).Bttlfront;
            ProgressingBattlefront EvC = (ProgressingBattlefront)GetRegion(11, false).Bttlfront;
            ProgressingBattlefront HEvDE = (ProgressingBattlefront)GetRegion(4, false).Bttlfront;*/

            // Evaluate if all three pairings are locked.
            if (DvG.PairingLocked && EvC.PairingLocked && HEvDE.PairingLocked)
            {
                Log.Debug("WorldMgr.EvaluateT4CampaignStatus", "*** ALL THREE PAIRINGS HAVE BEEN LOCKED ***");

                long _pairingLockTime = (30 * 60 * 1000);

                #if (DEBUG)
                    _pairingLockTime = (5 * 60 * 1000);
                #endif

                if (Constants.DoomsdaySwitch == 0)
                {
                    DvG.PairingUnlockTime = TCPManager.GetTimeStampMS() + _pairingLockTime;
                    EvC.PairingUnlockTime = TCPManager.GetTimeStampMS() + _pairingLockTime;
                    HEvDE.PairingUnlockTime = TCPManager.GetTimeStampMS() + _pairingLockTime;
                }

                ushort zone = 0;

                // If all the pairings are locked and Order owns all 3 pairings...
                if (DvG.GetZoneOwnership(3) == 1 && EvC.GetZoneOwnership(103) == 1 && HEvDE.GetZoneOwnership(203) == 1)
                {
                    lock (Player._Players)
                    {
                        foreach (Player plr in Player._Players)
                        {
                            if (!plr.ValidInTier(4, false) || plr.CurrentArea == null)
                                continue;

                            zone = plr.CurrentArea.ZoneId;

                            plr.SendLocalizeString("The forces of Order have beaten back their foes at every turn, cleansed their lands, and secured a time of peace! Unfortunately, they lack the resources to take the fight to the enemy's gates, and drive home their victory.", ChatLogFilters.CHATLOGFILTERS_RVR, Localized_text.CHAT_TAG_DEFAULT);
                            plr.SendLocalizeString("The forces of Order have beaten back their foes at every turn, cleansed their lands, and secured a time of peace! Unfortunately, they lack the resources to take the fight to the enemy's gates, and drive home their victory.", DvG.GetZoneOwnership(3) == (int)Realms.REALMS_REALM_ORDER ? ChatLogFilters.CHATLOGFILTERS_C_ORDER_RVR_MESSAGE : ChatLogFilters.CHATLOGFILTERS_C_DESTRUCTION_RVR_MESSAGE, Localized_text.CHAT_TAG_DEFAULT);
                            
                            if (plr.Realm == Realms.REALMS_REALM_ORDER && plr.CbtInterface.IsPvp && plr.CurrentArea.IsRvR && (zone == 3 || zone == 103 || zone == 203))
                                plr.ItmInterface.CreateItem(13000250, 1);
                            
                        }
                    }
                }
                // If all the pairings are locked and Destro owns all 3 pairings...
                else if (DvG.GetZoneOwnership(9) == 2 && EvC.GetZoneOwnership(109) == 2 && HEvDE.GetZoneOwnership(209) == 2)
                {
                    lock (Player._Players)
                    {
                        foreach (Player plr in Player._Players)
                        {
                            if (!plr.ValidInTier(4, false) || plr.CurrentArea == null)
                                continue;

                            zone = plr.CurrentArea.ZoneId;

                            plr.SendLocalizeString("The forces of Destruction have slaughtered, pillaged and razed a path into the very heartlands of their foes! But their infighting and the spoils of war slow them, and they lack the cohesion to subjugate their hated foes further.", ChatLogFilters.CHATLOGFILTERS_RVR, Localized_text.CHAT_TAG_DEFAULT);
                            plr.SendLocalizeString("The forces of Destruction have slaughtered, pillaged and razed a path into the very heartlands of their foes! But their infighting and the spoils of war slow them, and they lack the cohesion to subjugate their hated foes further.", DvG.GetZoneOwnership(3) == (int)Realms.REALMS_REALM_ORDER ? ChatLogFilters.CHATLOGFILTERS_C_ORDER_RVR_MESSAGE : ChatLogFilters.CHATLOGFILTERS_C_DESTRUCTION_RVR_MESSAGE, Localized_text.CHAT_TAG_DEFAULT);
                            
                            if (plr.Realm == Realms.REALMS_REALM_DESTRUCTION && plr.CbtInterface.IsPvp && plr.CurrentArea.IsRvR && (zone == 9 || zone == 109 || zone == 209))
                                plr.ItmInterface.CreateItem(13000249, 1);
                            
                        }
                    }
                }
                // If all the pairings are just locked
                else
                {
                    lock (Player._Players)
                    {
                        foreach (Player plr in Player._Players)
                        {
                            if (!plr.ValidInTier(4, false) || plr.CurrentArea == null)
                                continue;

                            zone = plr.CurrentArea.ZoneId;

                            plr.SendLocalizeString("The forces of Order and Destruction have traded blows all the way to the gates of their foes! But their supply lines are exposed, and the enemy threatens their back lines. Both are forced to abandon the victories, and pull back for a time.", ChatLogFilters.CHATLOGFILTERS_RVR, Localized_text.CHAT_TAG_DEFAULT);
                            plr.SendLocalizeString("The forces of Order and Destruction have traded blows all the way to the gates of their foes! But their supply lines are exposed, and the enemy threatens their back lines. Both are forced to abandon the victories, and pull back for a time.", ChatLogFilters.CHATLOGFILTERS_C_WHITE, Localized_text.CHAT_TAG_DEFAULT);
                        }
                    }
                }

                if(Constants.DoomsdaySwitch > 0)
                {
                    if (Region == 2) //DvG
                        Region = 12;
                    else if (Region == 4) //HEvDE
                        Region = 15;
                    else if (Region == 11) //EvC
                        Region = 14;

                    Random random = new Random();

                    if (Constants.DoomsdaySwitch == 2)
                    {
                        int newPairing = random.Next(1, 4);
                        ushort region = 12;

                        switch (newPairing)
                        {
                            case 1:
                                region = 12;
                                break;
                            case 2:
                                region = 14;
                                break;
                            case 3:
                                region = 15;
                                break;
                        }

                        while (region == Region)
                        {
                            newPairing = random.Next(1, 4);
                            switch (newPairing)
                            {
                                case 1:
                                    region = 12;
                                    break;
                                case 2:
                                    region = 14;
                                    break;
                                case 3:
                                    region = 15;
                                    break;
                            }
                        }

                        ProximityBattlefront bttlfrnt = (ProximityBattlefront)GetRegion(region, false).Bttlfront;
                        bttlfrnt.ResetPairing();

                        /*bool campaignReset = true;
                        for (int i = 0; i < 3; i++)
                        {
                            foreach (IBattlefront bf in BattlefrontList.RegionManagers[i])
                            {
                                ProximityBattlefront front = bf as ProximityBattlefront;
                                if (front != null && !front.PairingLocked)
                                {
                                    campaignReset = false;
                                    break;
                                }
                            }
                        }

                        if (campaignReset)
                        {
                            ProximityBattlefront bttlfrnt;

                            bttlfrnt = (ProximityBattlefront)GetRegion(12, false).Bttlfront;
                            bttlfrnt.ResetPairing();
                            bttlfrnt.UpdateStateOfTheRealm();
                            bttlfrnt = (ProximityBattlefront)GetRegion(14, false).Bttlfront;
                            bttlfrnt.ResetPairing();
                            bttlfrnt.UpdateStateOfTheRealm();
                            bttlfrnt = (ProximityBattlefront)GetRegion(15, false).Bttlfront;
                            bttlfrnt.ResetPairing();
                            bttlfrnt.UpdateStateOfTheRealm();
                        }*/
                    }
                    else
                    {
                        Battlefront bttlfrnt = (Battlefront)GetRegion(Region, false).Bttlfront;

                        foreach (Battlefront b in BattlefrontList.Battlefronts[1])
                        {
                            if (Constants.DoomsdaySwitch == 2)
                            {
                                b.ResetPairing();
                                b.UpdateStateOfTheRealm();
                            }
                            else
                            {
                                if (b != bttlfrnt)
                                {
                                    b.ResetPairing();
                                    b.UpdateStateOfTheRealm();
                                }
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region Keep registry, to remove it's static bullshit
        public static Dictionary<uint, Keep> _Keeps = new Dictionary<uint, Keep>();

        public static void SendKeepStatus(Player Plr)
        {
            foreach (List<Keep_Info> list in BattlefrontService._KeepInfos.Values)
            {
                foreach (Keep_Info KeepInfo in list)
                {
                    if (_Keeps.ContainsKey(KeepInfo.KeepId))
                    {
                        _Keeps[KeepInfo.KeepId].SendKeepStatus(Plr);
                    }
                    else
                    {
                        PacketOut Out = new PacketOut((byte)Opcodes.F_KEEP_STATUS, 26);
                        Out.WriteByte(KeepInfo.KeepId);
                        Out.WriteByte(1); // anything else explosion
                        Out.WriteByte(0); // ?
                        Out.WriteByte(KeepInfo.Realm);
                        Out.WriteByte(KeepInfo.DoorCount);
                        Out.WriteByte(0); // Rank
                        Out.WriteByte(100); // Door health
                        Out.WriteByte(0); // Next rank %
                        Out.Fill(0, 18);
                        Plr.SendPacket(Out);
                    }
                }
            }
        }
        #endregion
        
        #region Logging
        [LoadingFunction(true)]
        public static void ResetPacketLogSettings()
        {
            //turn off user specific packet logging when server restarts. This is because devs/gm forget to turn it off and log file grows > 20GB
            Log.Debug("WorldMgr", "Resetting user packet log settings...");
            Database.ExecuteNonQuery("update war_accounts.accounts set PacketLog = 0");
        }
        #endregion

        #region Other


        #endregion
    }
}
