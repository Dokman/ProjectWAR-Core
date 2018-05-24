﻿using System.Collections.Generic;
using GameData;
using SystemData;
using FrameWork;

namespace WorldServer.Managers.Commands
{
    public class GmMgr
    {
        private GmMgr() { }

        public static List<Player> GmList = new List<Player>();

        public static void NotifyGMOnline(Player gameMaster)
        {
            lock (GmList)
            {
                if (!GmList.Contains(gameMaster))
                    GmList.Add(gameMaster);
            }
        }

        public static void NotifyGMOffline(Player gameMaster)
        {
            lock (GmList)
                GmList.Remove(gameMaster);
        }

        public static bool ListGameMasters(Player plr, ref List<string> values)
        {
            lock (GmList)
            {
                if (GmList.Count == 0)
                    plr.SendClientMessage("[System] No GMs are currently online.", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
                else
                {
                    plr.SendClientMessage("[System] The following GMs are online(character - user):", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
                    foreach (Player player in GmList)
                        plr.SendClientMessage(player.Name + " - " + player.Client._Account.Username, ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
                    plr.SendClientMessage("Before messaging a GM, please verify that your issue cannot be solved by asking /advice or on the forum and that it truly merits messaging a GM.", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
                    plr.SendClientMessage("Remember - they're playing too.", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
                }
            }

            return true;
        }


        public static bool ToggleShowRank(Player plr, ref List<string> values)
        {
            plr.BroadcastRank = !plr.BroadcastRank;
            string rank = "";
            if (plr.BroadcastRank)
            {
                if (Utils.HasFlag(plr.GmLevel, (int)EGmLevel.DatabaseDev))
                    rank = "[DB]";
                if (Utils.HasFlag(plr.GmLevel, (int)EGmLevel.AnyGM))
                    rank = "[GM]";
                if (Utils.HasFlag(plr.GmLevel, (int)EGmLevel.SourceDev))
                    rank = "[Dev]";
                if (Utils.HasFlag(plr.GmLevel, (int)EGmLevel.Management))
                    rank = "[Lead]";

                PacketOut Out = new PacketOut((byte)Opcodes.F_UPDATE_LASTNAME);
                Out.WriteUInt16(plr.Oid);
                Out.WritePascalString(rank);
                plr.DispatchPacket(Out, true);

                plr.Info.Surname = rank;
            }
           if (!plr.BroadcastRank)
            {
                PacketOut Out = new PacketOut((byte)Opcodes.F_UPDATE_LASTNAME);
                Out.WriteUInt16(plr.Oid);
                Out.WritePascalString(rank);
                plr.DispatchPacket(Out, true);

                plr.Info.Surname = rank;
            }
            
            plr.SendClientMessage(plr.BroadcastRank ? "Your rank will now be shown in chat messages." : "Your rank will no longer be shown in chat messages.");

            return true;
        }
    }
}
