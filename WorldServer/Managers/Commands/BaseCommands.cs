﻿using System;
using System.Collections.Generic;
using System.Linq;
using SystemData;
using Common;
using FrameWork;
using GameData;
using WorldServer.Scenarios;
using static System.UInt16;
using static WorldServer.Managers.Commands.GMUtils;
using WorldServer.Services.World;
using WorldServer.World.Battlefronts;

namespace WorldServer.Managers.Commands
{
    /// <summary>
    /// Base commands invoked by players / gm when typing ".[command name]".
    /// </summary>
    internal static class BaseCommands
    {

        #region Functions
        public static bool HandleCommand(Player plr, string command, string text)
        {
            if (plr.Client._Account.GmLevel < 0 || text.Length <= 0)
                return true;

            string temp = text;

            if (text.Length > 4)
            {
                temp = text.Substring(0, 3);

                if (temp == "say")
                {
                    if (command == "/chan")
                    {
                        int spacePos = text.IndexOf(" ", 5);

                        if (spacePos != -1)
                            temp = text.Remove(0, spacePos < text.Length ? spacePos + 1 : spacePos);
                    }
                    else
                        temp = text.Substring(4, text.Length - 4);
                }

                else temp = text;
            }

            if (temp[0] != '.')
                return true;

            temp = temp.Remove(0, 1);
            List<string> values = new List<string>();

            //split by space and keep quoted strings together
            foreach (var v in System.Text.RegularExpressions.Regex.Split(temp, "(?<=^[^\"]*(?:\"[^\"]*\"[^\"]*)*) (?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)"))
                values.Add(v.ToString());

            values.RemoveAll(str => str.Length <= 0);

            return !DecodeCommand(plr, ref values, CommandDeclarations.BaseCommand, null);
        }
        public static bool DecodeCommand(Player plr, ref List<string> values, List<GmCommandHandler> handlers, List<GmCommandHandler> baseHandlers)
        {
            string command = GetString(ref values).ToLower();

            //Log.Success("DecodeCommand", "Command = " + Command);

            GmCommandHandler handler = handlers.Find(com => com != null && com.Name.StartsWith(command));

            if (handler == null) // Si la commande n'existe pas , on affiche la liste des commandes
            {
                List<GmCommandHandler> Base = handlers ?? baseHandlers;
                PrintCommands(plr, Base);
                plr.SendClientMessage(handler.Name.ToUpper() + ": This command not found", ChatLogFilters.CHATLOGFILTERS_USER_ERROR);
                return true;
            }
            if (handler.AccessRequired > 0 && (int)handler.AccessRequired > plr.GmLevel) // GmLevel insuffisant
            {
                plr.SendClientMessage(handler.Name.ToUpper() + ": This command has the access requirement of " + Enum.GetName(typeof(EGmLevel), handler.AccessRequired), ChatLogFilters.CHATLOGFILTERS_USER_ERROR);
                return false;
            }

            if (handler.ValueCount != -1 && handler.ValueCount > values.Count) // Nombre d'arguments insuffisant
            {
                plr.SendClientMessage(handler.Name.ToUpper() + ": Invalid argument count. (" + handler.Description + ")", ChatLogFilters.CHATLOGFILTERS_USER_ERROR);
                return true;
            }

            if (handler.Handlers != null && handler.Handlers.Count > 0)
            {
                return DecodeCommand(plr, ref values, handler.Handlers, handlers);
            }

            if (handler.Handler != null)
            {
                return handler.Handler.Invoke(plr, ref values);
            }

            return false;
        }
        #endregion

        #region Commands

        #region GM

        public static bool Invincible(Player plr, ref List<string> values)
        {
            Unit unit = plr.CbtInterface.GetTarget(TargetTypes.TARGETTYPES_TARGET_ALLY);

            if (unit == null || unit.IsPlayer())
                unit = plr;


            unit.IsInvulnerable = !unit.IsInvulnerable;
            plr.SendClientMessage("INVINCIBLE: " + unit.Name + ", " + unit.IsInvulnerable);


            if (unit.IsInvulnerable)
            {


                GMCommandLog log = new GMCommandLog
                {
                    PlayerName = plr.Name,
                    AccountId = (uint)plr.Client._Account.AccountId,
                    Command = "INVINCIBILITY TOGGLED on " + unit.Name,
                    Date = DateTime.Now
                };

                CharMgr.Database.AddObject(log);
            }

            else
            {


                GMCommandLog log = new GMCommandLog
                {
                    PlayerName = plr.Name,
                    AccountId = (uint)plr.Client._Account.AccountId,
                    Command = "INVINCIBILITY TOGGLED off " + unit.Name,
                    Date = DateTime.Now
                };

                CharMgr.Database.AddObject(log);
            }

            return true;
        }

        public static bool InvincibleMe(Player plr, ref List<string> values)
        {
            Unit unit = plr;


            unit.IsInvulnerable = !unit.IsInvulnerable;
            plr.SendClientMessage("INVINCIBLE: " + unit.Name + ", " + unit.IsInvulnerable);


            if (unit.IsInvulnerable)
            {
                string temp = "3";
                List<string> paramValue = temp.Split(' ').ToList();
                SetEffectStateSelf(plr, ref paramValue);

                GMCommandLog log = new GMCommandLog
                {
                    PlayerName = plr.Name,
                    AccountId = (uint)plr.Client._Account.AccountId,
                    Command = "INVINCIBILITY TOGGLED on " + unit.Name,
                    Date = DateTime.Now
                };

                CharMgr.Database.AddObject(log);
            }

            else
            {
                string temp = "3 0";
                List<string> paramValue = temp.Split(' ').ToList();
                SetEffectStateSelf(plr, ref paramValue);

                GMCommandLog log = new GMCommandLog
                {
                    PlayerName = plr.Name,
                    AccountId = (uint)plr.Client._Account.AccountId,
                    Command = "INVINCIBILITY TOGGLED off " + unit.Name,
                    Date = DateTime.Now
                };

                CharMgr.Database.AddObject(log);
            }

            return true;
        }

        public static bool SetFlightState(Player plr, ref List<string> values)
        {
            ushort npcupdateflag = 0;
            if (values.Count > 1)
            {
                TryParse(values[1], out npcupdateflag);
            }

            byte flag = (byte)GetInt(ref values);
            if (flag > 1)
                flag = 1;


            if (plr.CbtInterface.GetCurrentTarget() != null && plr.CbtInterface.GetCurrentTarget().IsCreature() && npcupdateflag != 0)
            {
                Creature cre = plr.CbtInterface.GetCurrentTarget().GetCreature();
                bool fly = false;
                if (flag == 1)
                    fly = true;

                cre.Faction = (byte)Utils.setBit(cre.Faction, 5, fly);
                cre.Spawn.Faction = (byte)Utils.setBit(cre.Spawn.Faction, 5, fly);
                WorldMgr.Database.SaveObject(cre.Spawn);

                if (npcupdateflag > 1)
                {
                    cre.Spawn.Proto.Faction = (byte)Utils.setBit(cre.Spawn.Proto.Faction, 5, fly);
                    cre.Spawn.Proto.Dirty = true;
                    WorldMgr.Database.SaveObject(cre.Spawn.Proto);
                }
                return true;
            }

            plr.SendUpdateState((byte)StateOpcode.Flight, flag, flag);

            if (flag == 1)
                plr.SendClientMessage("Flight access has been enabled. Use # to toggle flight mode, WASD to move, hold Jump to rise and hold Z to descend.");
            else
                plr.SendClientMessage("Flight access has been disabled.");
            return true;
        }

        public static bool SetDebugMode(Player plr, ref List<string> values)
        {
            plr.DebugMode = Convert.ToBoolean(GetInt(ref values));

            plr.SendClientMessage("Debug Mode: " + plr.DebugMode);

            return true;
        }
        public static bool PlaySound(Player plr, ref List<string> values)
        {
            bool server = false;
            int pos = 0;
            if (values.Count > 0)
            {
                if (values[0] == "server")
                {
                    pos++;
                    server = true;
                }

                ushort soundID = 0;
                if (UInt16.TryParse(values[pos], out soundID))
                    plr.PlaySound(soundID);
                else
                {
                    foreach (var t in values.Skip(pos))
                    {
                        var tokens = t.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                        if (tokens != null && tokens.Length == 4)
                        {
                            ushort delay = 0;
                            string speaker = tokens[0].Trim().Replace("\"", "");
                            string text = tokens[1].Trim().Replace("\"", "");
                            UInt16.TryParse(tokens[2], out soundID);
                            UInt16.TryParse(tokens[3], out delay);

                            if (delay != 0 && soundID != 0)
                            {
                                var prms = new List<object>() { speaker, text, soundID };

                                plr.EvtInterface.AddEvent((p) =>
                                {
                                    var Params = (List<object>)p;
                                    PacketOut Out = new PacketOut((byte)Opcodes.F_PLAY_SOUND, 30);
                                    Out.WriteByte(0);
                                    Out.WriteUInt16((ushort)Params[2]);
                                    Out.Fill(10, 0);

                                    if (server)
                                    {
                                        lock (Player._Players)
                                        {
                                            foreach (Player subPlayer in Player._Players)
                                            {

                                                if (Params[0].ToString().Length > 0 && Params[1].ToString().Length > 0)
                                                {
                                                    subPlayer.SendMessage(0, Params[0].ToString(), Params[1].ToString(), ChatLogFilters.CHATLOGFILTERS_MONSTER_SAY);
                                                    subPlayer.SendClientMessage(Params[1].ToString(), ChatLogFilters.CHATLOGFILTERS_C_ORANGE_L);
                                                }
                                                else if (Params[1].ToString().Length > 0)
                                                {
                                                    subPlayer.SendClientMessage(Params[1].ToString(), ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
                                                    subPlayer.SendClientMessage(Params[1].ToString(), ChatLogFilters.CHATLOGFILTERS_C_ORANGE_L);
                                                }
                                                subPlayer.SendPacket(Out);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        foreach (var subPlayer in plr.GetPlayersInRange(400, true))
                                        {
                                            if (Params[0].ToString().Length > 0 && Params[1].ToString().Length > 0)
                                            {
                                                subPlayer.SendMessage(0, Params[0].ToString(), Params[1].ToString(), ChatLogFilters.CHATLOGFILTERS_MONSTER_SAY);
                                                subPlayer.SendClientMessage(Params[1].ToString(), ChatLogFilters.CHATLOGFILTERS_C_WHITE_2);
                                            }
                                            else if (Params[1].ToString().Length > 0)
                                            {
                                                subPlayer.SendClientMessage(Params[1].ToString(), ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
                                                subPlayer.SendClientMessage(Params[1].ToString(), ChatLogFilters.CHATLOGFILTERS_C_WHITE_2);
                                            }
                                            subPlayer.SendPacket(Out);
                                        }
                                    }
                                }, delay * 1000, 1, prms);
                            }
                        }
                    }
                }
            }
            return true;
        }

        public static bool PlayEffect(Player plr, ref List<string> values)
        {
            if (values.Count > 1)
            {
                ushort effectID = 0;
                TryParse(values[1], out effectID);

                var playerName = GetString(ref values);


                Player target = Player.GetPlayer(playerName);

                if (target == null)
                {
                    plr.SendClientMessage("Your target is not a player.", ChatLogFilters.CHATLOGFILTERS_USER_ERROR);
                    return true;
                }

                if (plr.GmLevel == 0)
                {
                    plr.SendClientMessage("Use of the PlayEffect is not allowed for non-GM characters.", ChatLogFilters.CHATLOGFILTERS_USER_ERROR);
                    return true;
                }


                target.PlayEffect(effectID);


            }
            return true;
        }

        public static bool PlayAbility(Player plr, ref List<string> values)
        {
            if (values.Count > 1)
            {
                ushort abilityID = 0;
                ushort effectID = 0;

                TryParse(values[1], out abilityID);
                TryParse(values[2], out effectID);

                var playerName = GetString(ref values);


                Unit target = Player.GetPlayer(playerName);

                if (target == null)
                {
                    return true;
                }


                PacketOut Out = new PacketOut((byte)Opcodes.F_USE_ABILITY, 10);
                Out.WriteUInt16(0);
                Out.WriteUInt16(abilityID);
                Out.WriteUInt16(target.Oid);
                Out.WriteUInt16(effectID);
                Out.WriteUInt16(target.Oid);
                Out.WriteByte(2); //end cast
                Out.WriteByte(1); //end cast
                Out.WriteByte(0);
                Out.WriteByte(0); //result
                Out.WriteUInt16(0); //time
                Out.WriteByte(0); //sequence
                Out.WriteUInt16(0); //time
                Out.WriteByte(0);
                plr.DispatchPacket(Out, true);

                if (values.Count > 2)
                {
                    int duration = 0;
                    List<Unit> players = new List<Unit>();
                    players.Add(target);

                    foreach (var val in values)
                        if (val.ToUpper().StartsWith("AOE"))
                        {
                            try
                            {
                                int radius = 0;
                                int.TryParse(val.ToUpper().Replace("AOE", ""), out radius);
                                var inrange = target.GetInRange<Player>(radius).Where(e => e.Faction == target.Faction).ToList();
                                if (inrange != null && inrange.Count > 0)
                                    players.AddRange(inrange);
                            }
                            catch (Exception e)
                            {
                            }
                        }



                    if (int.TryParse(values[2], out duration))
                    {
                        ushort vfxID = 0;
                        for (int i = 3; i < values.Count; i++)
                        {


                            if (values[i].Contains("AOE"))
                                continue;

                            if (ushort.TryParse(values[i], out vfxID))
                            {

                                foreach (var t in players)
                                {
                                    Out = new PacketOut((byte)Opcodes.F_CAST_PLAYER_EFFECT, 200);

                                    Out.WriteUInt16(t.Oid);
                                    Out.WriteUInt16(t.Oid);
                                    Out.WriteUInt16(abilityID);
                                    Out.WriteByte((byte)vfxID);
                                    Out.WriteByte((byte)0); //parried, crit, dodged, etc
                                    Out.WriteByte((byte)1);
                                    Out.WriteByte((byte)0);
                                    plr.DispatchPacket(Out, true);

                                    var prms = new List<object>() { effectID, vfxID, t };

                                    plr.EvtInterface.AddEvent((p) =>
                                    {
                                        var efID = (ushort)prms[0];
                                        var vid = (ushort)prms[1];
                                        var t1 = (Player)prms[2];
                                        Out = new PacketOut((byte)Opcodes.F_CAST_PLAYER_EFFECT, 200);

                                        Out.WriteUInt16(t1.Oid);
                                        Out.WriteUInt16(t1.Oid);
                                        Out.WriteUInt16(abilityID);
                                        Out.WriteByte((byte)vid);
                                        Out.WriteByte((byte)0); //parried, crit, dodged, etc
                                        Out.WriteByte((byte)0);
                                        Out.WriteByte((byte)0);
                                        plr.DispatchPacket(Out, true);

                                    }, duration * 1000, 1, prms);
                                }
                            }

                        }
                    }
                }



            }

            return true;
        }


        private static void OnAttachEffect(object data)
        {

            var obj = (GameObject)((object[])data)[0];
            var effectId = (ushort)((object[])data)[1];
            var displayType = (ushort)((object[])data)[2];
            var effectInfo = (byte)((object[])data)[3];

            if (obj.Oid != 0)
            {
                PacketOut Out = new PacketOut((byte)Opcodes.F_CAST_PLAYER_EFFECT, 10);
                Out.WriteUInt16(obj.Oid);
                Out.WriteUInt16(obj.Oid);
                Out.WriteUInt16((ushort)effectId);
                Out.WriteByte((byte)displayType);
                Out.WriteByte(0); //result
                Out.WriteByte(effectInfo);
                Out.WriteByte(0);

                obj.DispatchPacket(Out, false);
            }
        }

        public static bool RemoveEffect(Player plr, ref List<string> values)
        {
            if (values.Count > 0)
            {
                ushort oid = 0;

                TryParse(values[0], out oid);

                var playerName = GetString(ref values);

                if (plr.GmLevel == 0)
                {
                    plr.SendClientMessage("Use of the RemoveEffect is not allowed for non-GM characters.", ChatLogFilters.CHATLOGFILTERS_USER_ERROR);
                    return true;
                }

                var invs = plr.Region.GetObject(oid);
                if (invs != null)
                    invs.RemoveFromWorld();
            }

            return true;
        }

        public static bool PreventCasting(Player plr, ref List<string> values)
        {
            AbilityInterface.PreventCasting = Convert.ToBoolean(GetInt(ref values));

            lock (Player._Players)
            {
                foreach (Player player in Player._Players)
                    player.SendClientMessage(AbilityInterface.PreventCasting ? "[System] A developer has temporarily disabled ability casting." : "[System] Abilities may once again be cast.", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
            }

            return true;
        }

        public static bool Shroud(Player plr, ref List<string> values)
        {
            switch (plr.StealthLevel)
            {
                case 0:
                    plr.Cloak(2);
                    plr.OSInterface.AddEffect(0x0C);
                    break;
                case 1:
                    plr.SendClientMessage("Shroud cannot be used if legitimate stealth is active.", ChatLogFilters.CHATLOGFILTERS_USER_ERROR);
                    break;
                case 2:
                    plr.Uncloak();
                    plr.OSInterface.RemoveEffect(0x0C);
                    break;
            }

            return true;
        }

        public static bool GetStats(Player plr, ref List<string> values)
        {
            Unit playerTarget = GetTargetOrMe(plr);

            if (playerTarget == null)
            {
                plr.SendClientMessage("Your target is not a player.", ChatLogFilters.CHATLOGFILTERS_USER_ERROR);
                return true;
            }

            if (playerTarget != plr && plr.GmLevel == 0)
            {
                if ((playerTarget != null && playerTarget is Pet && ((Pet)playerTarget).Owner == plr) && WorldMgr.WorldSettingsMgr.GetGenericSetting(20) == 0)
                {
                    plr.SendClientMessage("[Your Pet]\n", ChatLogFilters.CHATLOGFILTERS_TELL_RECEIVE);
                }
                else
                {
                    plr.SendClientMessage("Use of the GetStats command against other players is not allowed for non-GM characters.", ChatLogFilters.CHATLOGFILTERS_USER_ERROR);
                    return true;
                }
            }

            plr.SendClientMessage("[Stats for " + playerTarget.Name + "]\n", ChatLogFilters.CHATLOGFILTERS_TELL_RECEIVE);
            if (playerTarget is Creature && plr.GmLevel > 0)
            {
                Creature c = playerTarget as Creature;
                plr.SendClientMessage("Creature Career: " + c.Spawn.Proto.Career, ChatLogFilters.CHATLOGFILTERS_TELL_RECEIVE);
                plr.SendClientMessage("Creature Faction: " + c.Spawn.Faction, ChatLogFilters.CHATLOGFILTERS_TELL_RECEIVE);
            }
            for (ushort i = 0; i < (byte)Stats.MaxStatCount; i++)
            {
                Stats bonusType = (Stats)i;
                if (playerTarget.StsInterface.GetTotalStat(bonusType) != 0)
                    plr.SendClientMessage(bonusType + " " + playerTarget.StsInterface.GetTotalStat(bonusType) + " [(" + playerTarget.StsInterface.GetCoreStat(bonusType) + " + " + playerTarget.StsInterface.GetStatLinearModifier(bonusType) + ") x " + playerTarget.StsInterface.GetStatPercentageModifier(bonusType) + "]", ChatLogFilters.CHATLOGFILTERS_TELL_RECEIVE);
                else if (playerTarget.StsInterface.GetStatPercentageModifier(bonusType) != 1f)
                    plr.SendClientMessage(bonusType + " x" + playerTarget.StsInterface.GetStatPercentageModifier(bonusType), ChatLogFilters.CHATLOGFILTERS_TELL_RECEIVE);
            }

            plr.SendClientMessage("Item Stat Total: " + playerTarget.StsInterface.ItemStatTotal, ChatLogFilters.CHATLOGFILTERS_TELL_RECEIVE);
            plr.SendClientMessage("Speed: " + playerTarget.Speed, ChatLogFilters.CHATLOGFILTERS_TELL_RECEIVE);
            plr.SendClientMessage("Stats Speed: " + playerTarget.StsInterface.Speed, ChatLogFilters.CHATLOGFILTERS_TELL_RECEIVE);

            if (playerTarget is Player && plr.GmLevel > 0)
            {
                Player p = playerTarget as Player;
                plr.SendClientMessage("Current heal aggro: " + p.GetHealAggro(p.Oid).HealingReceived, ChatLogFilters.CHATLOGFILTERS_TELL_RECEIVE);
            }

            return true;
        }

        public static bool Announce(Player plr, ref List<string> values)
        {
            string message = GetTotalString(ref values);

            ushort soundID = 0x0c;

            if (values.Count > 1 && values[0] == "sound")
            {
                TryParse(values[1], out soundID);
                message = "";
                foreach (var s in values.Skip(2))
                    message += s + " ";
                message = message.Trim();
            }

            PacketOut Out = new PacketOut((byte)Opcodes.F_PLAY_SOUND);
            Out.WriteByte(0);
            Out.WriteUInt16(soundID);
            Out.Fill(0, 10);

            lock (Player._Players)
            {
                foreach (Player subPlayer in Player._Players)
                {

                    if (message.Length > 0)
                    {
                        subPlayer.SendClientMessage($"[Announcement][{plr.Client._Account.Username}]: {message}", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
                        subPlayer.SendClientMessage(message, ChatLogFilters.CHATLOGFILTERS_C_ORANGE_L);
                    }
                    subPlayer.SendPacket(Out);
                }
            }


            return true;
        }

        public static bool MessageAdvice(Player plr, ref List<string> values)
        {
            string message = GetTotalString(ref values);

            lock (Player._Players)
            {
                foreach (Player subPlayer in Player._Players)
                    if (subPlayer.Realm == plr.Realm)
                        subPlayer.SendMessage(0, "GM", message, ChatLogFilters.CHATLOGFILTERS_HELP_MESSAGE);
            }

            return true;
        }

        public static bool CSRMessage(Player plr, ref List<string> values)
        {
            string message = GetTotalString(ref values);

            lock (Player._Players)
            {
                foreach (Player subPlayer in Player._Players)
                    if (subPlayer.Realm == plr.Realm)
                        subPlayer.SendClientMessage($"[Staff][{plr.Client._Account.Username}]: {message}", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
            }

            return true;
        }

        public static bool Assist(Player plr, ref List<string> values)
        {
            string friendlyTarget = GetString(ref values);
            return true;
        }

        public static bool PacketLog(Player plr, ref List<string> values)
        {
            if (values.Count > 0)
            {
                string playerName = GetString(ref values);
                var player = CharMgr.GetCharacter(Player.AsCharacterName(playerName), false);

                if (player != null)
                {
                    var acct = Program.AcctMgr.GetAccountById(player.AccountId);

                    if (acct != null)
                    {
                        if (!acct.PacketLog)
                        {
                            acct.PacketLog = true;
                            plr.SendClientMessage("Account '" + acct.Username + "' for player '" + player.Name + "' is now being packet logged.");
                        }
                        else
                        {
                            acct.PacketLog = false;
                            plr.SendClientMessage("Disabled packet logging for account '" + acct.Username + "' for player '" + player.Name + "'");
                        }

                        Program.AcctMgr.UpdateAccount(acct);
                        plr.Client.PacketLog = acct.PacketLog;
                    }
                }
                else
                    plr.SendClientMessage("Player with name '" + values[0] + "' not found.", ChatLogFilters.CHATLOGFILTERS_USER_ERROR);
            }

            return true;

        }

        public static bool TellBlock(Player plr, ref List<string> values)
        {
            plr.SocInterface.blocksTells = !plr.SocInterface.blocksTells;

            plr.SendClientMessage(plr.SocInterface.blocksTells ? "Now blocking tells." : "No longer blocking tells.");

            return true;
        }

        #endregion

        #region Info

        public static bool ClearServer(Player plr, ref List<string> values)
        {
            foreach (Player player in Player._Players)
            {
                player.ForceSave();
                player.SendClientMessage("Server is being restarted right now...");
                if (player.GmLevel == 0)
                {
                    Object target = player;
                    player.EvtInterface.AddEvent(Kick, 2000, 1, target);
                }
            }

            return true;
        }

        public static void Kick(object plr)
        {
            Player player = plr as Player;
            if (plr != null)
            {
                PacketOut Out = new PacketOut((byte)Opcodes.F_PLAYER_QUIT, 4);
                Out.WriteHexStringBytes("01000000");
                player.SendPacket(Out);
            }
        }

        public static bool Reboot(Player plr, ref List<string> values)
        {
            Environment.Exit(0);
            return true;
        }

        public static bool Info(Player plr, ref List<string> values)
        {
            Object other = GetObjectTarget(plr);
            GameObject go = other as GameObject;
            Creature c = other as Creature;
            Player p = other as Player;
            if (go != null && go.IsGameObject())
            {
                string result = go.ToString();
                if (go.Spawn.TokUnlock != null && go.Spawn.TokUnlock != "0")
                    result = result + ", SpawnTokUnlock=" + go.Spawn.TokUnlock;
                if (go.Spawn.Proto.TokUnlock != null && go.Spawn.Proto.TokUnlock != "0")
                    result = result + ", ProtoTokUnlock=" + go.Spawn.Proto.TokUnlock;
                if (go.Spawn.Proto.TokUnlock != null && go.Spawn.Proto.TokUnlock != "0")
                    result = result + ", Realm=" + go.Realm;

                IList<Quest_Objectives> quests = WorldMgr.Database.SelectObjects<Quest_Objectives>("objtype = 3 AND objid = " + go.Entry);

                if (quests != null)
                {
                    foreach (Quest_Objectives quest in quests)
                    {
                        result = result + ", QuestEntry=" + quest.Entry.ToString();
                    }
                }

                IList<PQuest_Objective> pquests = WorldMgr.Database.SelectObjects<PQuest_Objective>("type = 3 AND objectid = " + go.Entry);
                if (pquests != null)
                {
                    foreach (PQuest_Objective quest in pquests)
                    {
                        result = result + ", PQEntry=" + quest.Entry.ToString();
                    }
                }

                plr.SendMessage(0, "", result, ChatLogFilters.CHATLOGFILTERS_EMOTE);
            }
            else if (c != null && c.IsCreature())
            {
                string result = c.ToString();
                if (c.Spawn.Proto.TokUnlock != null && c.Spawn.Proto.TokUnlock != "0")
                    result = result + ", TokUnlock=" + c.Spawn.Proto.TokUnlock;

                IList<Quest_Objectives> quests = WorldMgr.Database.SelectObjects<Quest_Objectives>("objtype = 2 AND objid = " + c.Entry);

                if (quests != null)
                {
                    foreach (Quest_Objectives quest in quests)
                    {
                        result = result + ", QuestEntry=" + quest.Entry.ToString();
                    }
                }

                IList<PQuest_Objective> pquests = WorldMgr.Database.SelectObjects<PQuest_Objective>("type = 2 AND objectid = " + c.Entry);

                if (pquests != null)
                {
                    foreach (PQuest_Objective quest in pquests)
                    {
                        result = result + ", PQEntry=" + quest.Entry.ToString();
                    }
                }

                plr.SendMessage(0, "", result, ChatLogFilters.CHATLOGFILTERS_EMOTE);

            }
            else if (p != null && p.IsPlayer())
            {
                plr.SendMessage(0, "", p.ToString(), ChatLogFilters.CHATLOGFILTERS_EMOTE);
            }
            else
            {
                plr.SendMessage(0, "", other.ToString(), ChatLogFilters.CHATLOGFILTERS_EMOTE);
            }

            return true;
        }

        public static bool AIInfo(Player player, ref List<string> values)
        {
            Creature crea = player.CbtInterface.GetCurrentTarget() as Creature;

            if (crea == null)
            {
                player.SendClientMessage("No creature targeted.");
                return true;
            }

            player.SendClientMessage($"[{crea.Name} - AI Info]");
            player.SendClientMessage($"Current Brain: {crea.AiInterface.CurrentBrain}");
            player.SendClientMessage($"AI State: {Enum.GetName(typeof(AiState), crea.AiInterface.State)}");
            player.SendClientMessage($"Current target: {crea.CbtInterface.GetCurrentTarget()?.Name ?? "None"}");
            player.SendClientMessage($"Movement state: {Enum.GetName(typeof(MovementInterface.EMoveState), crea.MvtInterface.MoveState)}");
            player.SendClientMessage($"Follow target: {crea.MvtInterface.FollowTarget?.Name ?? "None"}");

            return true;
        }
        public static bool MindRead(Player player, ref List<string> values)
        {
            Creature crea = player.CbtInterface.GetCurrentTarget() as Creature;

            if (crea == null)
            {
                player.SendClientMessage("No creature targeted.");
                return true;
            }

            if (!Utils.HasFlag(player.GmLevel, (int)EGmLevel.AllStaff) && (!(crea is Pet) || ((Pet)crea).Owner != player))
            {
                player.SendClientMessage("Non-staff members may only use this command to read the AI of their pet.");
                return true;
            }

            if (crea.AiInterface.Debugger == player)
            {
                crea.AiInterface.Debugger = null;
                player.SendClientMessage("Ceased mindreading " + crea.Name + ".");
            }

            else if (crea.AiInterface.Debugger == null)
            {
                crea.AiInterface.Debugger = player;
                player.SendClientMessage("Began mindreading " + crea.Name + ".");
            }

            else
            {
                player.SendClientMessage("A player is already mindreading " + crea.Name + ".");
            }

            return true;
        }

        public static bool PreviousPosition(Player player, ref List<string> values)
        {
            Player target = GetTargetOrMe(player) as Player;

            if (target == null)
            {
                player.SendClientMessage("PREVPOS: Requires a player target.");
                return true;
            }

            Point3D desiredPos = target.GetHistoricalPosition(TCPManager.GetTimeStampMS() - GetInt(ref values));

            player.SendClientMessage("Previous position: " + desiredPos);

            player.IntraRegionTeleport((uint)desiredPos.X, (uint)desiredPos.Y, (ushort)desiredPos.Z, 0);

            return true;
        }

        public static bool OverrideLatency(Player player, ref List<string> values)
        {
            int latency = GetInt(ref values);
            if (latency < 0 || latency > 600)
            {
                player.SendClientMessage("LATENCY: Value must be in range 0-600.");
                return true;
            }
            player.Latency = (ushort)latency;
            player.SendClientMessage("LATENCY: Your serverside latency is now " + player.Latency);
            return true;
        }

        #endregion

        #region Save

        public static bool Save(Player plr, ref List<string> values)
        {
            Player other = GetTargetOrMe(plr) as Player;
            plr.Save();
            return true;
        }

        #endregion

        #region Gps

        public static bool Gps(Player plr, ref List<string> values)
        {
            plr.UpdateWorldPosition();
            Object obj = plr.CbtInterface.GetCurrentTarget() ?? plr;

            string pos = "[" + obj.Name + "- GPS]";
            pos += "\nZone: " + obj.Zone.ZoneId + " " + obj.Zone.Info.Name;
            pos += "\nPin Position: " + obj.X + ", " + obj.Y + ", " + obj.Z;
            pos += "\nWorld Position: " + obj.WorldPosition.X + ", " + obj.WorldPosition.Y + ", " + obj.WorldPosition.Z;
            pos += "\nOffsets: X " + obj.XOffset + ", Y " + obj.YOffset;
            pos += "\nWh=" + obj.Heading + ",Ph=" + obj.Heading + ",HeightMap=" + ClientFileMgr.GetHeight(obj.Zone.ZoneId, obj.X, obj.Y);

            if (values.Count > 0 && GetString(ref values) == "say")
            {
                plr.Say(pos);
                if (obj != plr)
                {
                    plr.SendClientMessage(obj.ToString());
                    plr.SendClientMessage("Dist=" + plr.GetDistanceToObject(obj));
                }
            }

            else
            {
                plr.SendClientMessage(pos);
                if (obj != plr)
                {
                    plr.SendClientMessage(obj.ToString());
                    plr.SendClientMessage("Dist=" + plr.GetDistanceToObject(obj));
                }
            }

            return true;
        }
        #endregion

        private static AbilityKnockbackInfo _jumpbackInfo = new AbilityKnockbackInfo { Angle = 75, Power = 200, GravMultiplier = 2 };

        public static bool Unlock(Player plr, ref List<string> values)
        {
            if (!plr.WasGrounded)
            {
                plr.SendClientMessage("Unlocking is not allowed when falling");
                return false;
            }


            if (plr.NextJumpTime < TCPManager.GetTimeStampMS())
            {
                if (plr.CbtInterface.IsInCombat)
                    return true;

                plr.NextJumpTime = TCPManager.GetTimeStampMS() + 30000;

                plr.ApplySelfKnockback(_jumpbackInfo);
            }

            else plr.SendClientMessage("Next unlocking attempt allowed in " + (plr.NextJumpTime - TCPManager.GetTimeStampMS()) / 1000 + " seconds.");

            return true;
        }

        public static bool SetSurname(Player plr, ref List<string> values)
        {
            if (String.IsNullOrEmpty(values[0]))
                return true;

            if (values[0] == "clear")
                values[0] = "";

            /*
            PacketOut Out = new PacketOut((byte)Opcodes.F_UPDATE_LASTNAME);
            Out.WriteUInt16(plr.Oid);
            Out.WritePascalString(values[0]);
            plr.DispatchPacket(Out, true);

            plr.Info.Surname = values[0];*/
            plr.SetLastName(values[0]);
            return true;
        }

        public static bool SendMovementDebug(Player plr, ref List<string> values)
        {
            Creature crea = plr.CbtInterface.GetCurrentTarget() as Creature;

            if (crea != null)
            {
                //crea.MvtInterface.SendMovementDebug(plr);
                return true;
            }

            Player player = plr.CbtInterface.GetCurrentTarget() as Player;

            player?.StsInterface.CheckVelocityModifiers(plr);

            return true;
        }

        public static bool CheckDoorId(Player plr, ref List<string> values)
        {
            PacketOut Out = new PacketOut((byte)Opcodes.F_CREATE_STATIC);

            Out.WriteHexStringBytes("02D8");
            Out.WriteUInt16((ushort)GetInt(ref values));
            Out.WriteHexStringBytes("0000444B0006B9CE00026AFFFFFF1E000000BB8500040400000000640005C7270000000004446F6F7204");

            var uniqueID = (uint)(((0x08B73600 >> 6) & 0x3FFF)) | GetInt(ref values);

            plr.SendClientMessage("Testing ID: " + uniqueID);

            Out.WriteUInt32((uint)((plr.Zone.ZoneId << 20) + (uniqueID << 6) + 0x28));

            plr.SendClientMessage("Testing DoorID: " + (uint)((plr.Zone.ZoneId << 20) + (uniqueID << 6) + 0x28));

            plr.SendPacket(Out);

            return true;
        }

        public static bool CreateRvRObject(Player plr, ref List<string> values)
        {
            RvRStructure.CreateNew(plr, GetInt(ref values));

            return true;
        }

        public static bool MasteryTesting(Player plr, ref List<string> values)
        {
            PacketOut Out = new PacketOut((byte)Opcodes.F_CAREER_CATEGORY);

            /*
            Out.WritePacketString(@"|07 01 00 03 00 00 00 00 00 00 17 70 00 |.S............p.|
|00 75 30 0D 53 6F 72 63 65 72 65 72 20 53 70 65 |.u0.Sorcerer Spe|
|63 00 18 00 01 00 02 00 03 00 04 00 05 00 06 00 |c...............|
|07 00 08 00 09 00 0A 00 0B 00 0C 00 0D 00 0E 00 |................|
|0F 00 10 00 11 00 12 00 13 00 14 00 15 00 16 00 |................|
|17 00 18 00 00 00                               |......          |");
            Plr.SendPacket(Out);
            */



            Out.WritePacketString(@"|07 01 00 1D 00 00 00 00 00 00 E2 90 00 |.S..............|
    |01 38 80 0D 53 6F 72 63 65 72 65 72 20 53 70 65 |.8..Sorcerer Spe|
    |63 00 18 00 01 00 02 00 03 00 04 00 05 00 06 00 |c...............|
    |07 00 08 00 09 00 0A 00 0B 00 0C 00 0D 00 0E 00 |................|
    |0F 00 10 00 11 00 12 00 13 00 14 00 15 00 16 00 |................|
    |17 00 18 00 00 00                               |......          |");
            plr.SendPacket(Out);


            Out = new PacketOut((byte)Opcodes.F_CAREER_PACKAGE_INFO);
            /*
            Out.WritePacketString(@"|07 01 00 01 00 00 00 00 04 02 01 00 00 |.*..............|
|00 00 00 00 00 00 00 00 00 00 00 01 01 00 00 02 |................|
|0E 06 00 00 00 00 00 0F 00 00 00 00 00          |.............   |");
            Plr.SendPacket(Out);
             */

            //spec tree 1

            Out = new PacketOut((byte)Opcodes.F_CAREER_PACKAGE_INFO);
            Out.WritePacketString(@"|07 01 00 01 00 00 00 00 0D 02 00 00 00 |.*..............|
    |00 00 00 00 00 00 00 00 00 00 00 01 01 00 00 02 |................|
    |0E 06 00 00 00 00 00 0F 00 00 00 00 00          |.............   |");
            plr.SendPacket(Out);
            /*
            Out.WritePacketString(@"|07 01 00 02 00 00 00 00 00 02 00 00 00 |.*..............|
            |00 00 00 00 00 00 00 00 00 00 00 01 01 00 00 02 |................|
            |0F 06 00 00 00 00 00 0F 00 00 00 00 00          |.............   |
            ");
                         Plr.SendPacket(Out);
                         */

            //spec tree 2

            Out = new PacketOut((byte)Opcodes.F_CAREER_PACKAGE_INFO);
            Out.WritePacketString(@"|07 01 00 02 00 00 00 00 0A 02 00 00 00 |.*..............|
    |00 00 00 00 00 00 00 00 00 00 00 01 01 00 00 02 |................|
    |0F 06 00 00 00 00 00 0F 00 00 00 00 00          |.............   |");
            plr.SendPacket(Out);

            Out = new PacketOut((byte)Opcodes.F_CAREER_PACKAGE_INFO);
            /*
            Out.WritePacketString(@"|07 01 00 03 00 00 00 00 00 02 00 00 00 |.*..............|
|00 00 00 00 00 00 00 00 00 00 00 01 01 00 00 02 |................|
|10 06 00 00 00 00 00 0F 00 00 00 00 00          |.............   |");
            Plr.SendPacket(Out);
            */

            //spec tree 3

            Out = new PacketOut((byte)Opcodes.F_CAREER_PACKAGE_INFO);
            Out.WritePacketString(@"|07 01 00 03 00 00 00 00 00 02 00 00 00 |.*..............|
    |00 00 00 00 00 00 00 00 00 00 00 01 01 00 00 02 |................|
    |10 06 00 00 00 00 00 0F 00 00 00 00 00          |.............   |");
            plr.SendPacket(Out);


            // adds spells to the mastery tree
            //Plr.SendMasterySkills();


            // adds core spells to mastery tree propably alos to the career trainer
            Out = new PacketOut((byte)Opcodes.F_CAREER_CATEGORY);


            Out.WritePacketString(@"| 00 01 00 00 00 00 00 00 00 00 E2 90 00 |.L..............|
    |01 38 80 12 53 6F 72 63 65 72 65 72 20 41 62 69 |.8..Sorcerer Abi|
    |6C 69 74 69 65 73 00 12 00 01 00 02 00 03 00 04 |lities..........|
    |00 05 00 06 00 07 00 08 00 09 00 0A 00 0B 00 0C |................|
    |00 0D 00 0E 00 0F 00 10 00 11 00 12 00 00 00    |............... |");
            plr.SendPacket(Out);

            Out = new PacketOut((byte)Opcodes.F_CAREER_PACKAGE_INFO);
            Out.WritePacketString(@"|00 01 00 01 00 02 00 00 01 01 00 00 00 |.H..............|
    |00 00 00 00 00 00 00 00 00 00 00 00 00 00 14 01 |................|
    |00 00 14 01 02 00 00 25 00 00 01 0A 2A 01 04 B0 |.......%....*...|
    |28 01 00 00 00 00 00 00 00 03 E8 0A 47 6C 6F 6F |(...........Gloo|
    |6D 62 75 72 73 74 00 00 00 00 00                |...........     |");
            plr.SendPacket(Out);


            Out = new PacketOut((byte)Opcodes.F_CAREER_PACKAGE_INFO);
            Out.WritePacketString(@"|00 01 00 0F 00 19 00 00 01 01 00 00 00 |.K..............|
    |00 00 00 00 00 00 00 00 00 00 00 00 00 0C 35 01 |..............5.|
    |00 00 14 0E 02 00 00 25 0D 00 01 0A 48 01 03 C0 |.......%....H...|
    |0F 06 00 00 00 00 00 00 00 00 00 0D 50 69 74 20 |............Pit |
    |6F 66 20 53 68 61 64 65 73 00 00 00 00 00       |..............  |
    ");
            plr.SendPacket(Out);


            // sends renown spells to renown trainer




            // Plr.SendRenownDefensiveCrittsSkills();



            //Plr.SendInfluenceInfo();
            /*

            Plr.Region.Bttlfront.addkill((int)Plr.Realm);
            WorldMgr.GetRegion(3, false).Bttlfront.addscwin(2);
            WorldMgr.SendCampaignStatus(null);
            */

            return true;
        }

        public static bool Packet(Player plr, ref List<string> values)
        {
            Unit target = plr.CbtInterface.GetCurrentTarget();

            if (target == plr || target == null)
                return false;

            PacketOut Out = new PacketOut((byte)Opcodes.F_CATAPULT);
            Out.WriteUInt16(plr.Oid);
            Out.WriteUInt16(plr.Zone.ZoneId);
            Out.WriteUInt16((ushort)plr.X); // Start X
            Out.WriteUInt16((ushort)plr.Y); // Start Y
            Out.WriteUInt16((ushort)plr.Z); // Start Z
            Out.WriteUInt16(plr.Zone.ZoneId);
            Out.WriteUInt16((ushort)target.X); // Destination X
            Out.WriteUInt16((ushort)target.Y); // Destination Y
            Out.WriteUInt16((ushort)target.Z); // Destination Z
            Out.WriteUInt16(0x012C);
            Out.WriteByte(01);
            Out.WriteHexStringBytes("00000000000000000000000000000000000000"); // Terrible Embrace.
            plr.DispatchPacket(Out, true);
            return true;
        }

        public static bool Packet2(Player plr, ref List<string> values)
        {
            Unit target = plr.CbtInterface.GetCurrentTarget();

            PacketOut Out = new PacketOut((byte)Opcodes.F_KEEP_STATUS, 26);

            Out.WriteByte(2); // MoM Destro Keep
            Out.WriteByte(1); // Keep Status
            Out.WriteByte(1); // ?
            //Out.WriteByte((byte)Realm);
            Out.WriteByte((byte)GetInt(ref values));
            Out.WriteByte(0); // Number of doors
            Out.WriteByte(0); // Rank
            Out.WriteByte(0); // Door Health
            Out.WriteByte(0); // Next rank %
            Out.Fill(0, 18);

            lock (Player._Players)
                foreach (Player player in Player._Players)
                    player.SendCopy(Out);
            return true;
        }

        public static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                            .Where(x => x % 2 == 0)
                            .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                            .ToArray();
        }

        #endregion

        #region DeathKill

        public static bool Revive(Player plr, ref List<string> values)
        {
            Unit target = plr.CbtInterface.GetCurrentTarget();

            if (target == null)
                target = plr;

            if (!target.IsDead)
            {
                plr.SendClientMessage("Can't revive a living target.", ChatLogFilters.CHATLOGFILTERS_USER_ERROR);
                return true;
            }

            target.RezUnit();

            if (target.IsPlayer())
            {
                GMCommandLog log = new GMCommandLog();
                log.PlayerName = plr.Name;
                log.AccountId = (uint)plr.Client._Account.AccountId;
                log.Command = "REZ PLAYER " + target.Name;
                log.Date = DateTime.Now;
                CharMgr.Database.AddObject(log);

                if (target != plr)
                {
                    ((Player)target).SendLocalizeString(plr.Name, ChatLogFilters.CHATLOGFILTERS_SAY, Localized_text.TEXT_GM_RESURRECTED);
                    plr.SendLocalizeString(target.Name, ChatLogFilters.CHATLOGFILTERS_SAY, Localized_text.TEXT_GM_RESURRECT_SUCCESS);
                }
            }

            return true;
        }

        public static bool AssignStandard(Player plr, ref List<string> values)
        {

            if (!plr.GldInterface.IsInGuild())
                return false;

            String Playername = "";
            if (values.Count > 0)
                Playername = values[0];

            if (Playername.Length < 1)
                Playername = plr.Info.Name;

            plr.GldInterface.Guild.SetStandardBearer(plr, Playername);

            return true;
        }

        public static bool Kill(Player player, ref List<string> values)
        {
            if (AbilityInterface.PreventCasting)
            {
                player.SendClientMessage("KILL: This command does not function when ability casting is blocked.", ChatLogFilters.CHATLOGFILTERS_USER_ERROR);
                return true;
            }

            Unit target = player.CbtInterface.GetCurrentTarget();

            if (target == null || target.IsDead)
            {
                player.SendClientMessage("KILL: The target is null or already dead.", ChatLogFilters.CHATLOGFILTERS_USER_ERROR);
                return true;
            }

            if (target.IsPlayer())
            {
                if (!Utils.HasFlag(player.GmLevel, (int)EGmLevel.TrustedStaff))
                {
                    player.SendClientMessage("KILL: Using this command on players requires trusted staff access.", ChatLogFilters.CHATLOGFILTERS_USER_ERROR);
                    return true;
                }
            }

            GMCommandLog log = new GMCommandLog
            {
                PlayerName = player.Name,
                AccountId = (uint)player.Client._Account.AccountId,
                Command = "KILL PLAYER " + target.Name,
                Date = DateTime.Now
            };

            CharMgr.Database.AddObject(log);

            target.ReceiveDamage(player, int.MaxValue);

            PacketOut damageOut = new PacketOut((byte)Opcodes.F_CAST_PLAYER_EFFECT, 24);

            damageOut.WriteUInt16(player.Oid);
            damageOut.WriteUInt16(target.Oid);
            damageOut.WriteUInt16(23584); // Terminate

            damageOut.WriteByte(0);
            damageOut.WriteByte(0); // DAMAGE EVENT
            damageOut.WriteByte(7);

            damageOut.WriteZigZag(-30000);
            damageOut.WriteByte(0);

            target.DispatchPacketUnreliable(damageOut, true, player);

            return true;
        }

        public static bool Nuke(Player player, ref List<string> values)
        {
            int realm = 0;
            int radius = 0;

            if (values.Count > 1)
            {
                realm = Convert.ToInt32(values[0]);
                radius = Convert.ToInt32(values[1]);
            }
            else
            {
                player.SendClientMessage("Command failed, you need to provide realm (0 - all, 1 - order, 2 - destruction) and radius.", ChatLogFilters.CHATLOGFILTERS_TELL_RECEIVE);
                return false;
            }

            int i = 0;

            foreach (Player plr in player.GetPlayersInRange(radius))
            {
                if (!Utils.HasFlag(plr.GmLevel, (int)EGmLevel.Staff) && ((int)plr.Realm == realm || realm == 0))
                {
                    plr.ReceiveDamage(player, int.MaxValue);

                    PacketOut damageOut = new PacketOut((byte)Opcodes.F_CAST_PLAYER_EFFECT, 24);

                    damageOut.WriteUInt16(player.Oid);
                    damageOut.WriteUInt16(plr.Oid);
                    damageOut.WriteUInt16(23584); // Terminate

                    damageOut.WriteByte(0);
                    damageOut.WriteByte(0); // DAMAGE EVENT
                    damageOut.WriteByte(7);

                    damageOut.WriteZigZag(-30000);
                    damageOut.WriteByte(0);

                    plr.DispatchPacketUnreliable(damageOut, true, player);

                    plr.SendClientMessage("You have been nuked by " + player.Name + "!", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
                    i++;
                }
            }

            GMCommandLog log = new GMCommandLog
            {
                PlayerName = player.Name,
                AccountId = (uint)player.Client._Account.AccountId,
                Command = player.Name + " NUKED " + i + " PLAYERS",
                Date = DateTime.Now
            };

            CharMgr.Database.AddObject(log);

            return true;
        }

        #endregion

        #region Toks
        public static bool AllTok(Player plr, ref List<string> values)
        {

            for (ushort i = 1; i < 11999; i++)
            {
                plr.TokInterface.AddTok(i);
            }


            return true;
        }

        public static bool AllTokbestary(Player plr, ref List<string> values)
        {

            for (int i = 1; i < 1000; i++)
            {
                plr.TokInterface.SendActionCounterUpdate((ushort)i, (uint)i);
            }
            return true;
        }
        #endregion

        #region GChat Interception

        public static bool LogGuild(Player plr, ref List<string> values)
        {
            string guildName = GetTotalString(ref values);

            Guild guild = Guild.GetGuild(guildName);
            if (guild == null)
            {
                plr.SendClientMessage("Guild " + guildName + " doesn't exist.");
                return true;
            }

            guild.StartLogging();
            return true;
        }

        public static bool CancelLogGuild(Player plr, ref List<string> values)
        {
            string guildName = GetTotalString(ref values);

            Guild guild = Guild.GetGuild(guildName);
            if (guild == null)
            {
                plr.SendClientMessage("Guild " + guildName + " doesn't exist.");
                return true;
            }

            guild.EndLogging();
            return true;
        }

        #endregion

        public static bool ScoutChamps(Player plr, ref List<string> values)
        {
            RegionMgr region = plr.Region;

            foreach (Object obj in region.Objects)
            {
                Creature crea = obj as Creature;

                if (crea == null || crea.Realm == plr.Realm || crea.Rank != 1)
                    continue;

                plr.SendClientMessage(crea.Spawn.Proto.Name + " Level " + crea.Level + " at X " + crea.X + " Y " + crea.Y + " Z " + crea.Z + " in zone " + crea.Zone.Info.Name);
            }

            return true;
        }

        public static bool SetCoreTester(Player plr, ref List<string> values)
        {
            string playerName = GetString(ref values);

            Player target = Player.GetPlayer(playerName);

            Account account = GetAccountForPlayer(playerName);

            if (account == null)
            {
                plr.SendClientMessage("The specified player does not exist.");
                return true;
            }

            if (account.GmLevel > 0)
            {
                plr.SendClientMessage(playerName + " is a staff member.");
                return true;
            }
            if (account.CoreLevel == 0)
            {
                account.CoreLevel = 1;
                CharMgr.Database.SaveObject(account);
                CharMgr.Database.ForceSave();
                plr.SendClientMessage("ADDED 'Core Tester' status set for " + playerName);
            }
            else
            {
                account.CoreLevel = 0;
                CharMgr.Database.SaveObject(account);
                CharMgr.Database.ForceSave();
                plr.SendClientMessage("REMOVED 'Core Tester' status set for " + playerName);
            }
            return true;
        }

        public static bool SetEffectState(Player plr, ref List<string> values)
        {
            if (values.Count > 0)
            {
                var player = plr;
                if (plr.CbtInterface.GetCurrentTarget() is Player)
                    player = (Player)plr.CbtInterface.GetCurrentTarget();

                byte effectID = 0;
                bool enabled = true;
                if (byte.TryParse(values[0], out effectID))
                {

                    if (values.Count > 1)
                    {
                        enabled = false;
                        if (player.EffectStates.Contains(effectID))
                            player.EffectStates.Remove(effectID);
                    }
                    else if (!player.EffectStates.Contains(effectID))
                        player.EffectStates.Add(effectID);


                    var Out = new PacketOut((byte)Opcodes.F_OBJECT_EFFECT_STATE);

                    Out.WriteUInt16(player.Oid);
                    Out.WriteByte(1);
                    Out.WriteByte(effectID);
                    Out.WriteByte((byte)(enabled ? 1 : 0));
                    Out.WriteByte(0);

                    player.DispatchPacket(Out, true);
                }
            }
            return true;
        }

        public static bool SetEffectStateSelf(Player plr, ref List<string> values)
        {
            if (values.Count > 0)
            {
                var player = plr;

                byte effectID = 0;
                bool enabled = true;
                if (byte.TryParse(values[0], out effectID))
                {

                    if (values.Count > 1)
                    {
                        enabled = false;
                        if (player.EffectStates.Contains(effectID))
                            player.EffectStates.Remove(effectID);
                    }
                    else if (!player.EffectStates.Contains(effectID))
                        player.EffectStates.Add(effectID);


                    var Out = new PacketOut((byte)Opcodes.F_OBJECT_EFFECT_STATE);

                    Out.WriteUInt16(player.Oid);
                    Out.WriteByte(1);
                    Out.WriteByte(effectID);
                    Out.WriteByte((byte)(enabled ? 1 : 0));
                    Out.WriteByte(0);

                    player.DispatchPacket(Out, true);
                }
            }
            return true;
        }

        public static bool GetObjects(Player plr, ref List<string> values)
        {
            string objs = "";
            foreach (var obj in plr.ObjectsInRange.Where(e => e is GameObject).ToList().OrderBy(e => plr.GetDistanceTo(e.WorldPosition)))
            {
                objs += obj.Oid + ": " + obj.Name + " (distance=" + (plr.GetDistanceTo(obj.WorldPosition) / 12f) + ")\n";
            }
            plr.SendClientMessage(objs);
            return true;
        }

        // This is OptOut workaround with an addon, need more work
        public static bool Optout(Player plr, ref List<string> values)
        {
            string tier = values[0];
            string optOutType = values[1];
            if (tier == "t3")
            {
                if (optOutType == "all")
                {

                }
                else if (optOutType == "gold")
                {

                }
            }
            else if (tier == "t4")
            {

            }
            return false;
        }

        public static bool SetVfxState(Player plr, ref List<string> values)
        {
            if (values.Count > 0)
            {

                int vfx = 0;
                int oid = 0;

                int.TryParse(values[0], out oid);
                int.TryParse(values[1], out vfx);

                if (oid != 0)
                {
                    var obj = plr.GetObjectInRange(oid);
                    if (obj is GameObject)
                    {
                        ((GameObject)obj).VfxState = (byte)vfx;
                    }
                }
            }
            return true;
        }

        public static bool SetNpcModel(Player plr, ref List<string> values)
        {
            if (values.Count > 0)
            {
                var target = plr;
                if (plr.CbtInterface.GetCurrentTarget() is Player)
                    target = (Player)plr.CbtInterface.GetCurrentTarget();

                if (target != plr && target.GmLevel >= plr.GmLevel)
                {
                    plr.SendClientMessage("SETNPCMODEL: This command will only function on players with a lower staff rank than your own.");
                    return true;
                }

                int modelID = 0;
                int.TryParse(values[0], out modelID);
                if (target != null)
                {
                    target.ImageNum = (ushort)modelID;

                    var Out = new PacketOut((byte)Opcodes.F_PLAYER_IMAGENUM); //F_PLAYER_INVENTORY
                    Out.WriteUInt16(target.Oid);
                    Out.WriteUInt16((ushort)modelID);
                    Out.Fill(0, 18);
                    target.DispatchPacket(Out, true);
                }
            }
            return true;
        }
        public static bool CreatePlayer(Player plr, ref List<string> values)
        {
            if (plr != null && plr.CbtInterface.GetCurrentTarget() != null)
            {
                var Out = new PacketOut((byte)Opcodes.F_REMOVE_PLAYER); //F_PLAYER_INVENTORY
                Out.WriteUInt16(plr.Oid);
                Out.WriteUInt16((ushort)plr.CbtInterface.GetCurrentTarget().Oid);
                Out.Fill(0, 18);
                plr.DispatchPacket(Out, true);
                plr.CbtInterface.GetCurrentTarget().SendMeTo(plr);
            }
            return true;
        }
        public static bool PreviewItemModel(Player plr, ref List<string> values)
        {
            if (values.Count > 1)
            {
                int slotID = 0;
                int modelID = 0;

                int.TryParse(values[0], out slotID);
                int.TryParse(values[1], out modelID);

                var item = plr.ItmInterface.GetItemInSlot((ushort)slotID);
                if (item != null)
                {
                    item.PreviewModelID = (uint)modelID;

                    var Out = new PacketOut(0xAA);
                    if (item != null)
                    {
                        Out.WriteByte(1);
                        Out.Fill(0, 3);
                        Item.BuildItem(ref Out, item, null, null, (ushort)slotID, 0, plr);
                        var pos = Out.Position;
                        Out.Position = 14;
                        Out.WriteUInt16((ushort)modelID);
                        Out.Position = pos;
                        plr.SendPacket(Out);

                        Out = new PacketOut(0xBD); //F_PLAYER_INVENTORY
                        Out.WriteUInt16(plr.Oid);
                        Out.WriteUInt16(1);
                        Out.WriteUInt16((ushort)slotID);
                        Out.WriteUInt16((ushort)modelID);
                        Out.WriteByte(0);
                        plr.DispatchPacket(Out, false);
                    }
                }
            }
            return true;
        }

        public static bool ObjectiveState(Player plr, ref List<string> values)
        {
            if (values.Count > 1)
            {
                int oid = 0;
                int state = 0;

                int.TryParse(values[0], out oid);
                int.TryParse(values[1], out state);

                var Out = new PacketOut((byte)Opcodes.F_UPDATE_STATE, 18);
                Out.WriteUInt16((byte)oid);
                Out.WriteByte(6);
                Out.WriteByte(0);
                Out.WriteUInt16(0x08);

                Out.WriteUInt16((byte)state);

                Out.Fill(0, 10);
                plr.DispatchPacket(Out, true);

                return true;
            }
            return false;
        }

        public static bool SetCollationLength(Player plr, ref List<string> values)
        {
            Program.Config.PacketCollateLength = Math.Max(0, Math.Min(GetInt(ref values), 32768));

            plr.SendClientMessage(Program.Config.PacketCollateLength == 0 ? "No longer collating packets." : $"Packets will be collated until {Program.Config.PacketCollateLength} bytes.", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);

            return true;
        }

        public static bool XpMode(Player plr, ref List<string> values)
        {
            uint characterId = plr.GetPlayer().CharacterId;
            var target = plr;

            if (target._Value.XpMode == 0)
            {
                target._Value.XpMode = 1;
                plr.SendClientMessage("No longer gaining experience points.");
            }
            else
            {
                target._Value.XpMode = 0;
                plr.SendClientMessage("Now gaining experience points.");
            }

            return true;
        }

        public static bool ScenarioCmd(Player plr, ref List<string> values)
        {
            if (plr.ScnInterface.Scenario != null)
            {
                plr.ScnInterface.Scenario.GmCommand(plr, ref values);
            }

            return true;
        }

        public static bool FindIP(Player plr, ref List<string> values)
        {
            string ipMatch = values[0];

            plr.SendClientMessage("IP matches for " + ipMatch + ":");

            lock (Player._Players)
            {
                foreach (Player player in Player._Players)
                {
                    if (player.Client == null)
                        continue;

                    if (player.Client.GetIp().StartsWith(ipMatch))
                        plr.SendClientMessage(player.Name);
                }
            }

            return true;
        }

        #region Character Access

        public static bool GetGuildLead(Player plr, ref List<string> values)
        {
            if (values.Count < 1)
            {
                plr.SendClientMessage("Usage: .getguildlead guildname");
                return true;
            }
            string guildName = GetTotalString(ref values);

            if (string.IsNullOrEmpty(guildName))
            {
                plr.SendClientMessage("GETGUILDLEAD: Must specify a guildname");
                return true;
            }

            Guild gld = Guild.GetGuild(guildName);
            if (gld == null)
            {
                plr.SendClientMessage("Guild " + guildName + " does not exist");
                return true;
            }
            else
            {
                string leader = Player.GetPlayer(gld.Info.LeaderId).Name;
                plr.SendClientMessage("Guild " + guildName + " current leader is: " + leader);
                return true;
            }
        }

        public static bool GetGuildID(Player plr, ref List<string> values)
        {
            if (values.Count < 1)
            {
                plr.SendClientMessage("Usage: .getguildid guildname");
                return true;
            }
            string guildName = GetTotalString(ref values);

            if (string.IsNullOrEmpty(guildName))
            {
                plr.SendClientMessage("GETGUILDID: Must specify a guildname");
                return true;
            }

            Guild gld = Guild.GetGuild(guildName);
            if (gld == null)
            {
                plr.SendClientMessage("Guild " + guildName + " does not exist.");
                return true;
            }
            else
            {
                plr.SendClientMessage("Guild: " + guildName + " ID: " + gld.Info.GuildId);
                return true;
            }
        }
        /// <summary>
        /// removes a guild from its alliance
        /// </summary>
        /// <param name="plr"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public static bool ForceAllianceQuit(Player plr, ref List<string> values)
        {
            if (values.Count < 2)
            {
                plr.SendClientMessage("Usage: .forcealliancequit <guildID>");
                return true;
            }

            uint guildID = (uint)(int)GetInt(ref values);

            Guild guild = Guild.GetGuild(guildID);

            if (guild == null)
            {
                plr.SendClientMessage("The Specified guild does not exist");
                return true;
            }

            if (guild.Info.AllianceId == 0)
            {
                plr.SendClientMessage("The Guild specified does not belong to an alliance");
                return true;
            }

            else
            {
                plr.SendClientMessage("Removing " + guild.Info.Name + " from its alliance");

                guild.LeaveAlliance();

                GMCommandLog log = new GMCommandLog
                {
                    PlayerName = plr.Name,
                    AccountId = (uint)plr.Client._Account.AccountId,
                    Command = "KICKED GUILD OUT OF ALLIANCE: " + guild.Info.Name,
                    Date = DateTime.UtcNow
                };
                CharMgr.Database.AddObject(log);

                return true;
            }

        }

        public static bool GetCharSlots(Player plr, ref List<string> values)
        {
            if (values.Count < 1)
            {
                plr.SendClientMessage("Usage: .getcharslots accountName");
                return true;
            }

            string accountName = GetString(ref values);

            if (string.IsNullOrEmpty(accountName))
            {
                plr.SendClientMessage("GETCHARSLOTS: Must specify an account ID.");
                return true;
            }

            Account acct = Program.AcctMgr.GetAccount(accountName);

            if (acct == null)
            {
                plr.SendClientMessage("GETCHARSLOTS: Nonexistent account " + accountName);
                return true;
            }

            AccountChars chars = CharMgr.GetAccountChar(acct.AccountId);

            if (chars == null)
            {
                plr.SendClientMessage($"GETCHARSLOTS: {accountName} has no characters.");
                return true;
            }

            plr.SendClientMessage($"Characters for {accountName}:");

            for (int i = 0; i < chars.Chars.Length; ++i)
            {
                Character curChar = chars.Chars[i];

                if (curChar == null)
                    continue;

                if (curChar.Value == null)
                    plr.SendClientMessage($"Slot {i}: {curChar.Name}, {Constants.CareerNames[curChar.CareerLine]} (Missing characters_value!)");
                else
                    plr.SendClientMessage($"Slot {i}: {curChar.Name}, {curChar.Value.Level}/{curChar.Value.RenownRank} {Constants.CareerNames[curChar.CareerLine]}");
            }
            return true;
        }


        private static List<Tuple<uint, int, long>> _deletionRequests = new List<Tuple<uint, int, long>>();

        public static bool DeleteCharInSlot(Player developer, ref List<string> values)
        {
            if (values.Count < 2)
            {
                developer.SendClientMessage("Usage: .deletecharat accountName slotid");
                return true;
            }

            string accountName = GetString(ref values);

            if (string.IsNullOrEmpty(accountName))
            {
                developer.SendClientMessage("DELETECHARAT: Must specify an account ID.");
                return true;
            }

            Account acct = Program.AcctMgr.GetAccount(accountName);

            if (acct == null)
            {
                developer.SendClientMessage("DELETECHARAT: Nonexistent account " + accountName);
                return true;
            }

            if (acct.GmLevel > 1)
            {
                developer.SendClientMessage("DELETECHARAT: Can't delete staff characters.");
                return true;
            }

            AccountChars chars = CharMgr.GetAccountChar(acct.AccountId);

            if (chars == null)
            {
                developer.SendClientMessage($"DELETECHARAT: {acct.Username} has no characters.");
                return true;
            }

            byte slotId = (byte)GetInt(ref values);

            Character chara = chars.GetCharacterBySlot(slotId);

            if (chara == null)
            {
                developer.SendClientMessage($"DELETECHARAT: {acct.Username} has no character at slot {slotId}.");
                return true;
            }

            if (chara.Value.Online)
            {
                developer.SendClientMessage($"DELETECHARAT: {chara.Name} is currently online.");
                return true;
            }

            for (int i = 0; i < _deletionRequests.Count; ++i)
            {
                if (_deletionRequests[i].Item1 == developer.Info.CharacterId)
                {
                    if (_deletionRequests[i].Item2 != acct.AccountId)
                    {
                        _deletionRequests.RemoveAt(i);
                        --i;
                        continue;
                    }

                    long timeToAccept = _deletionRequests[i].Item3 - TCPManager.GetTimeStampMS();
                    if (timeToAccept > 0)
                    {
                        developer.SendClientMessage("DELETECHARAT: You must wait " + timeToAccept + "ms before you can confirm a deletion request.");
                        return true;
                    }

                    developer.SendClientMessage($"DELETECHARAT: Removing from {acct.Username} the character named {chara.Name} at slot {slotId}.");

                    CharMgr.RemoveCharacter(developer, acct.AccountId, slotId);

                    _deletionRequests.RemoveAt(i);
                    return true;
                }
            }

            _deletionRequests.Add(new Tuple<uint, int, long>(developer.CharacterId, acct.AccountId, TCPManager.GetTimeStampMS() + 5000));

            developer.SendClientMessage("DELETECHARAT: You have requested to delete the character " + chara.Name + ". Please confirm by reentering the command in 5 seconds.");

            return true;
        }

        #endregion

        #region Motd

        public static bool SetMotd(Player plr, ref List<string> values)
        {
            string motd = GetTotalString(ref values);

            Program.Config.Motd = motd;

            foreach (Player pPlr in Player._Players)
            {
                pPlr.SendLocalizeString(Program.Config.Motd, ChatLogFilters.CHATLOGFILTERS_CITY_ANNOUNCE, Localized_text.TEXT_SERVER_MOTD);
            }

            return true;
        }

        public static bool SendRules(Player plr, ref List<string> values)
        {
            plr.SendClientMessage("===================================== Warhammer Online  =====================================", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
            plr.SendClientMessage("These rules are a short version of the ones written on the forum and serve as a warning.", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
            plr.SendClientMessage("Because of this, our staff are under no obligation whatsoever to issue a warning before taking action against any player.\n", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);

            plr.SendClientMessage("[General Rules]", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
            plr.SendClientMessage("Don't use our game server to break the law. Making any kind of profit from this project is forbidden.", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
            plr.SendClientMessage("Anyone executing any kind of denial of service attack will be banned.", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
            plr.SendClientMessage("Multiboxing is NOT permitted.", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);

            plr.SendClientMessage("[Interacting with Staff]", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
            plr.SendClientMessage("Any player displaying a [GM] or [Dev] tag is a staff member. While displaying such a tag, that person's word is law, and their instructions are to be followed, immediately, without comment or complaint.", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
            plr.SendClientMessage("Don't mess, in any way, with staff members who are displaying [GM] or [Dev] tags. This includes by making snarky, whining, demeaning or patronizing comments in chat.", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
            plr.SendClientMessage("Staff actions such as kicks, bans and mutes are to be appealed via forum private message ONLY.", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
            plr.SendClientMessage("Violating any of the above rules will ALWAYS result in being exiled (temporary ban).\n", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);

            plr.SendClientMessage("[Chat]", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
            plr.SendClientMessage("Don't harass and flame other players.\nDon't disrupt the chat (with spam, permanent caps lock, excessive swearing, etc.)", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
            plr.SendClientMessage("Both use of text colors and icon spam are banned in all global chats.", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
            plr.SendClientMessage("No cheating accusations. Report suspected cheaters to a GM via the forum.\nNo hate speech or other objectionable content.", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
            plr.SendClientMessage("No advertisements (for Twitch, TeamSpeak, products and services, etc.)\nNo doxxing.\n", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);

            plr.SendClientMessage("[Channel Use]", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
            plr.SendClientMessage("Use the right chat channel.\nAdvice is for game help and support ONLY.\nTrade (/4) is the ONLY channel for trades.", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
            plr.SendClientMessage("LFG (/5) is the ONLY channel for group and guild recruitment/advertisement.\nOff-Topic (/6) is for non-Warhammer related chat.\nGeneral (/3) handles most other subjects.\n", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);

            plr.SendClientMessage("[Ban Evasion]", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
            plr.SendClientMessage("Making a separate account to dodge a ban will result in your ban length being increased.", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
            plr.SendClientMessage("Allowing a ban evader to play on your account will result in that account being permanently banned.", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
            plr.SendClientMessage("Knowingly assisting a ban evader will result in a ban.\n", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);

            plr.SendClientMessage("[Cheating]", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
            plr.SendClientMessage("Don't use hacks, cheats and other illegitimate game modifications.", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
            plr.SendClientMessage("Don't automate your gameplay by any means (macros, bots etc.)", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
            plr.SendClientMessage("If your character is acting in the game, you MUST be able to respond to a GM if prompted.", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
            plr.SendClientMessage("Don't use any addon which creates issues or allows access to exploits.", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
            plr.SendClientMessage("Don't intentionally profit from an exploit carried out by another player.", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
            plr.SendClientMessage("We, the team, reserve the right to neutralize the proceeds of any exploit (by deleting items, etc.)", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
            plr.SendClientMessage("Sometimes, we can screw up and create bugs and glitches which are highly exploitable. Exploiting these issues is cheating and will be treated as cheating.\n", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);

            return true;
        }

        public static bool RoRFeatures(Player plr, ref List<string> values)
        {
            string arg1 = GetString(ref values).ToLower();

            switch (arg1)
            {
                case "debolster":
                    plr.SendClientMessage("===================================== Features: Debolster =====================================\n", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
                    plr.SendClientMessage("Get debolstered and play in lower tiers by meeting ALL of the following requirements:", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
                    plr.SendClientMessage("Tier 1: No more than 2 mastery points spent, no more than 13 renown points spent, no equipped gear above level 13 and no talismans in gear.", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
                    plr.SendClientMessage("Tier 2/3: No more than 13 mastery points spent, no more than 28 renown points spent, and no equipped gear above level 28", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
                    break;
                case "balance":
                    plr.SendClientMessage("===================================== Features: Balance Changes =====================================\n", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
                    plr.SendClientMessage("There have been several changes to Career and Combat mechanics.", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
                    plr.SendClientMessage("Careers which have undergone global balance changes notify you of this when you log them in.", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
                    plr.SendClientMessage("You may enter the command .ab changelist to display a list of changes for such a career.", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
                    plr.SendClientMessage("You can also see a list of changes on the forums.", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
                    plr.SendClientMessage("Minor Changes: In Progress", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
                    plr.SendClientMessage("Balance Forums: In Progress", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
                    plr.SendClientMessage("General changes:", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
                    plr.SendClientMessage("Resurrection Illness now applies a 25% stat debuff (down from 50%, still excluding Wounds)", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
                    break;
                /*case "exmode":
                    plr.SendClientMessage("=====================================\nRoR Features: Experimental Mode\n=====================================\n", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
                    plr.SendClientMessage("Some careers have Experimental Modes, which contain more major class reworks.", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
                    plr.SendClientMessage("Careers with Experimental Mode notify you of this when you log them in.", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
                    plr.SendClientMessage("You may enter the command .ab ex to activate or deactivate such experimental modes.", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
                    plr.SendClientMessage("Doing so will also show a list of changes.", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
                    break;*/
                case "items":
                    plr.SendClientMessage("=====================================\nRoR Features: New Items and Sets\n=====================================\n", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
                    plr.SendClientMessage("Most item sets have had their stats adjusted to be more favorable.", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
                    plr.SendClientMessage("Other item sets have been adjusted for reasons of balance.", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
                    plr.SendClientMessage("There are too many changes to list here, but in general, do not trust old sources to be accurate.", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
                    break;
                default:
                    plr.SendClientMessage("===================================== Features =====================================\n", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
                    plr.SendClientMessage("Information about unique features", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
                    plr.SendClientMessage("Usage: .ror (debolster|balance|items|exmode)", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
                    break;
            }

            return true;
        }

        public static bool TokFix(Player plr, ref List<string> values)
        {
            plr.SendClientMessage("This command is disabled.", SystemData.ChatLogFilters.CHATLOGFILTERS_USER_ERROR);

            /*Dictionary<uint, List<Character_tok_kills>> charToksKills = CharMgr.Database.SelectAllObjects<Character_tok_kills>().Where(x => x.CharacterId == plr.CharacterId).GroupBy(v => v.CharacterId).ToDictionary(g => g.Key, g => g.ToList());

            plr.TokInterface.FixTokItems();

            foreach (Character_tok_kills kill in charToksKills[plr.CharacterId])
            {
                if (kill != null)
                { 
                    Tok_Bestary TB = WorldMgr.Database.SelectObject<Tok_Bestary>("bestary_id = " + kill.NPCEntry);
                    if (TB != null)
                        plr.TokInterface.CheckTokKills(TB.Creature_Sub_Type,kill.Count);
                }
            }*/

            return true;
        }

        public static bool RvRStatus(Player plr, ref List<string> values)
        {
            string s = "";
            for (int i = 0; i < 4; i++)
            {
                if (i == 0)
                {
                    foreach (World.Battlefronts.RoRBattlefront b in World.Battlefronts.BattlefrontList.Battlefronts[i])
                    {
                        if (!b.PairingLocked)
                        {
                            s = "T1 " + b.ActiveZoneName + " is open.";
                            plr.SendClientMessage(s, SystemData.ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
                        }
                    }
                }
                else
                {
                    if (Constants.DoomsdaySwitch == 2)
                    {
                        foreach (ProximityBattlefront b in World.Battlefronts.BattlefrontList.Battlefronts[i])
                        {
                            if (!b.PairingLocked)
                            {
                                s = "T" + b.Region.GetTier() + " " + b.ActiveZoneName + " is open.";
                                if (b.ActiveSupplyLine == 1)
                                {
                                    s = s + " Supply lines are active in this zone.\n";
                                    s = s + "Order Realm Rank: " + b.RealmRank[0].ToString();
                                    s = s + " Destruction Realm Rank: " + b.RealmRank[1].ToString();
                                }
                                if (b.PairingDrawTime != 0)
                                {
                                    s = s + "\nThis zone will draw in " + ((int)((b.PairingDrawTime - TCPManager.GetTimeStamp()) / 60)).ToString() + " minutes.";
                                }
                                plr.SendClientMessage(s, SystemData.ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
                            }
                        }
                    }
                    else
                    {
                        foreach (Battlefront b in World.Battlefronts.BattlefrontList.Battlefronts[i])
                        {
                            if (!b.PairingLocked)
                            {
                                s = "T" + b.Region.GetTier() + " " + b.ActiveZoneName + " is open.";
                                if (b.ActiveSupplyLine == 1)
                                    s = s + " Supply lines are active in this zone.";
                                plr.SendClientMessage(s, SystemData.ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
                            }
                        }
                    }
                }
            }

            return true;
        }

#if DEBUG
        public static bool GearTester(Player plr, ref List<string> values)
        {
            Unit target = plr.CbtInterface.GetCurrentTarget();
            Player player = target as Player;
            if (player == null || !player.IsPlayer())
                player = plr;

            // Setting gold
            int money = 10000000;
            player.AddMoney((uint)money);

            // Setting SC and RvR currencies
            Item_Info conqMedallion = ItemService.GetItem_Info(1698);
            Item_Info conqEmblem = ItemService.GetItem_Info(1699);
            Item_Info ruin = ItemService.GetItem_Info(129838571);
            Item_Info genesis1 = ItemService.GetItem_Info(486);
            Item_Info genesis2 = ItemService.GetItem_Info(487);
            Item_Info genesis3 = ItemService.GetItem_Info(488);
            Item_Info genesis4 = ItemService.GetItem_Info(489);
            Item_Info genesis5 = ItemService.GetItem_Info(490);
            Item_Info warrant1 = ItemService.GetItem_Info(498);
            Item_Info warrant2 = ItemService.GetItem_Info(499);

            // Scrolls
            Item_Info gunbadScroll = ItemService.GetItem_Info(65826);
            Item_Info scroll = null;
            if (player.Realm == Realms.REALMS_REALM_ORDER)
                scroll = ItemService.GetItem_Info(12983325);
            else
                scroll = ItemService.GetItem_Info(12983324);

            // Setting potions and talismans
            Item_Info officersElixer1 = ItemService.GetItem_Info(208291);
            Item_Info officersElixer2 = ItemService.GetItem_Info(208292);
            Item_Info draughtRecovery = ItemService.GetItem_Info(3000209);
            Item_Info quickeningDraught = ItemService.GetItem_Info(3000406);
            Item_Info lastingPower = ItemService.GetItem_Info(3000649);
            Item_Info lastingDiscipline = ItemService.GetItem_Info(3001049);
            Item_Info lastingBrilliance = ItemService.GetItem_Info(3000849);
            Item_Info lastingVerity = ItemService.GetItem_Info(3001249);
            Item_Info lastingUnguent = ItemService.GetItem_Info(197552);
            Item_Info taliChampion = ItemService.GetItem_Info(907197);
            Item_Info taliPolymath = ItemService.GetItem_Info(907317);
            Item_Info taliTenacity = ItemService.GetItem_Info(907357);
            Item_Info taliMartyrdom = ItemService.GetItem_Info(907477);
            Item_Info taliAssassin = ItemService.GetItem_Info(907237);
            Item_Info taliAggressor = ItemService.GetItem_Info(907277);
            Item_Info taliGladiator = ItemService.GetItem_Info(907397);
            Item_Info taliPrevarication = ItemService.GetItem_Info(907437);
            Item_Info taliSmith = ItemService.GetItem_Info(907037);

            player.ItmInterface.CreateItem(conqMedallion, 5000);
            player.ItmInterface.CreateItem(conqEmblem, 5000);
            player.ItmInterface.CreateItem(ruin, 100);
            player.ItmInterface.CreateItem(genesis1, 100);
            player.ItmInterface.CreateItem(genesis2, 100);
            player.ItmInterface.CreateItem(genesis3, 100);
            player.ItmInterface.CreateItem(genesis4, 100);
            player.ItmInterface.CreateItem(genesis5, 100);
            player.ItmInterface.CreateItem(warrant1, 100);
            player.ItmInterface.CreateItem(warrant2, 100);
            player.ItmInterface.CreateItem(officersElixer1, 20);
            player.ItmInterface.CreateItem(officersElixer2, 20);
            player.ItmInterface.CreateItem(draughtRecovery, 20);
            player.ItmInterface.CreateItem(quickeningDraught, 20);
            player.ItmInterface.CreateItem(lastingPower, 20);
            player.ItmInterface.CreateItem(lastingDiscipline, 20);
            player.ItmInterface.CreateItem(lastingBrilliance, 20);
            player.ItmInterface.CreateItem(lastingVerity, 20);
            player.ItmInterface.CreateItem(lastingUnguent, 20);
            player.ItmInterface.CreateItem(taliChampion, 20);
            player.ItmInterface.CreateItem(taliPolymath, 20);
            player.ItmInterface.CreateItem(taliTenacity, 20);
            player.ItmInterface.CreateItem(taliMartyrdom, 20);
            player.ItmInterface.CreateItem(taliAssassin, 20);
            player.ItmInterface.CreateItem(taliAggressor, 20);
            player.ItmInterface.CreateItem(taliGladiator, 20);
            player.ItmInterface.CreateItem(taliPrevarication, 20);
            player.ItmInterface.CreateItem(taliSmith, 20);
            player.ItmInterface.CreateItem(gunbadScroll, 20);
            player.ItmInterface.CreateItem(scroll, 1);

            // Setting level
            int level = 40;
            player.SetLevel((byte)level);

            // Setting renown
            int renownLevel = 45;

            Character chara = CharMgr.GetCharacter(player.Name, false);

            if (chara == null)
            {
                plr.SendClientMessage($"MODIFY RENOWN: The player {player.Name} in question does not exist.");
                return true;
            }

            int desiredRenownRank = renownLevel > 0 ? renownLevel : Math.Max(1, chara.Value.RenownRank + renownLevel);
            desiredRenownRank = Math.Min(100, desiredRenownRank);

            if (player != null)
                player.SetRenownLevel((byte)desiredRenownRank);
            else
            {
                chara.Value.Renown = 0;
                chara.Value.RenownRank = (byte)desiredRenownRank;
                CharMgr.Database.SaveObject(chara.Value);
            }

            if (player != plr)
                plr.SendClientMessage($"MODIFY RENOWN: {player.Name}'s renown rank is now {chara.Value.RenownRank}.");

            return true;
        }

        public static bool Gunbad(Player player, ref List<string> values)
        {
            player.Teleport(2, 1241652, 897090, 7499, 0);

            return true;
        }
#endif

        public static bool QuestComplete(Player plr, ref List<string> values)
        {
            ushort QuestId = 0;
            ushort.TryParse(values[0], out QuestId);
            ushort Command = 0;
            ushort.TryParse(values[1], out Command);

            Unit target = plr.CbtInterface.GetCurrentTarget();
            Player player = target as Player;
            if (player == null || !player.IsPlayer())
                player = plr;

            if (Command == 1) // Adds quest to player DB
            {
                if (player != null && player.IsPlayer() && QuestId != 0)
                {
                    player.QtsInterface.AcceptQuest(QuestId);
                }
            }
            else if (Command == 2) // Finish Quest
            {
                if (player != null && player.IsPlayer() && QuestId != 0)
                {
                    if (player.QtsInterface.GetQuest(QuestId) != null)
                    {
                        Character_quest quest = player.QtsInterface.GetQuest(QuestId);

                        foreach (KeyValuePair<ushort, Character_quest> questKp in player.QtsInterface.Quests)
                        {
                            if (questKp.Value == quest)
                            {
                                foreach (Character_Objectives objective in questKp.Value._Objectives)
                                {
                                    if (objective.Objective == null)
                                        continue;

                                    objective.Count = (int)objective.Objective.ObjCount;
                                    questKp.Value.Dirty = true;
                                    player.QtsInterface.SendQuestUpdate(questKp.Value);
                                    CharMgr.Database.SaveObject(questKp.Value);

                                    if (objective.IsDone())
                                    {
                                        Creature finisher;

                                        foreach (Object obj in player.ObjectsInRange)
                                        {
                                            if (obj.IsCreature())
                                            {
                                                finisher = obj.GetCreature();
                                                if (QuestService.HasQuestToFinish(finisher.Entry, questKp.Value.Quest.Entry))
                                                    finisher.SendMeTo(player.GetPlayer());
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else if (Command == 3) // Delete quest from player DB
            {
                if (player != null && player.IsPlayer() && QuestId != 0)
                {
                    if (player.QtsInterface.GetQuest(QuestId) != null)
                    {
                        Character_quest characterQuest = player.QtsInterface.GetQuest(QuestId);
                        characterQuest.Dirty = true;
                        CharMgr.Database.DeleteObject(characterQuest);
                        player.QtsInterface.AbandonQuest(QuestId);
                    }
                }
            }
            else
            {
                plr.SendClientMessage("Provided incorrect value - only 1, 2 or 3 is accepted.", ChatLogFilters.CHATLOGFILTERS_TELL_RECEIVE);
            }
            return true;
        }

        public static bool SoREnable(Player plr, ref List<string> values)
        {
            plr.SoREnabled = true;
            for (int i = 0; i < 4; i++)
            {
                if (i == 0)
                {
                    foreach (RoRBattlefront b in BattlefrontList.Battlefronts[i])
                        b.UpdateStateOfTheRealm();
                }
                else
                {
                    if (Constants.DoomsdaySwitch == 2)
                        foreach (ProximityBattlefront b in BattlefrontList.Battlefronts[i])
                            b.UpdateStateOfTheRealm();
                    else
                        foreach (Battlefront b in BattlefrontList.Battlefronts[i])
                            b.UpdateStateOfTheRealm();
                }
            }
            plr.SendClientMessage("State of the Realm Addon Enabled: 1.0.3", SystemData.ChatLogFilters.CHATLOGFILTERS_SAY);
            return true;
        }
        public static bool PugScenario(Player plr, ref List<string> values)
        {
            plr.SendClientMessage("The current pickup scenario is " + ScenarioMgr.PickupScenarioName + ".", ChatLogFilters.CHATLOGFILTERS_SCENARIO);
            return true;
        }

        public static bool BolsterLevel(Player plr, ref List<string> values)
        {
            if (Constants.DoomsdaySwitch > 0)
            {
                if (values.Count < 1)
                {
                    plr.SendClientMessage("BOLSTERLEVEL: No value provided.", ChatLogFilters.CHATLOGFILTERS_USER_ERROR);
                    return true;
                }

                World_Settings Settings = WorldMgr.Database.SelectObject<World_Settings>("SettingId = 1");

                Settings.Setting = GetInt(ref values);
                WorldMgr.Database.SaveObject(Settings);
                WorldMgr.Database.ForceSave();

                foreach (Player player in Player._Players)
                {
                    if (player.Level > 15 && player.AdjustedLevel > 15 && player.AdjustedLevel < 40 && player.CbtInterface.IsPvp && player.Zone.Info.Type == 0 && plr.ScnInterface.Scenario == null)
                    {
                        player.RemoveBolster();
                        player.TryBolster(4, player.CurrentArea);
                    }
                }
            }
            else
            {
                plr.SendClientMessage("This command is available only during DoomsDay T2 T3 T4 merge event.", SystemData.ChatLogFilters.CHATLOGFILTERS_USER_ERROR);
            }

            return true;
        }

        public static bool ChangePlayerDrop(Player plr, ref List<string> values)
        {
            if (values.Count < 1)
            {
                plr.SendClientMessage("PLAYERDROP: No value provided.", ChatLogFilters.CHATLOGFILTERS_USER_ERROR);
                return true;
            }
            int value = GetInt(ref values);
            if (value == 0 || value == 1)
            {
                WorldMgr.WorldSettingsMgr.SetMedallionsSetting(value);
                if (value == 1)
                    plr.SendClientMessage("Alternative RvR Medallion drop active.", ChatLogFilters.CHATLOGFILTERS_TELL_RECEIVE);
                else
                    plr.SendClientMessage("Alternative RvR Medallion drop disabled.", ChatLogFilters.CHATLOGFILTERS_TELL_RECEIVE);
            }
            else
                plr.SendClientMessage("PLAYERDROP: Incorrect value", ChatLogFilters.CHATLOGFILTERS_USER_ERROR);

            return true;
        }

        public static bool KeepReward(Player plr, ref List<string> values)
        {
            if (values.Count < 1)
            {
                plr.SendClientMessage("KEEPREWARD: No value provided.", ChatLogFilters.CHATLOGFILTERS_USER_ERROR);
                return true;
            }
            int value = GetInt(ref values);
            if (value == 0 || value == 1)
            {
                WorldMgr.WorldSettingsMgr.SetPopRewardSwitchSetting(value);
                if (value == 1)
                {
                    plr.SendClientMessage("Low pop keep rewards limits are enabled.", ChatLogFilters.CHATLOGFILTERS_TELL_RECEIVE);
                }
                else
                {
                    plr.SendClientMessage("Low pop keep rewards limits are disabled.", ChatLogFilters.CHATLOGFILTERS_TELL_RECEIVE);
                    if (Constants.DoomsdaySwitch == 2)
                        ((ProximityBattlefront)plr.Region.Bttlfront).DefenderPopTooSmall = false;
                }


            }
            else
                plr.SendClientMessage("KEEPREWARD: Incorrect value, 0 - disabled, 1 - enabled", ChatLogFilters.CHATLOGFILTERS_USER_ERROR);

            return true;
        }

        public static bool RequestNameChange(Player plr, ref List<string> values)
        {
            if (values.Count < 1)
            {
                plr.SendClientMessage("Usage: .requestnamechange new_player_name", ChatLogFilters.CHATLOGFILTERS_USER_ERROR);
                return true;
            }

            IList<AccountSanctionInfo> sanctions = Program.AcctMgr.GetSanctionsFor(plr.Info.AccountId);

            foreach (var sanction in sanctions)
            {
                if (sanction.ActionType == "Exile" || sanction.ActionType == "Permanent Ban")
                {
                    plr.SendClientMessage("Name changes are not available to anyone who has previously been banned.", ChatLogFilters.CHATLOGFILTERS_USER_ERROR);
                    return true;
                }
            }

            if (plr.RenownRank < 40)
            {
                plr.SendClientMessage("A character must be at or above Renown Rank 40 in order to change its name.", ChatLogFilters.CHATLOGFILTERS_USER_ERROR);
                return true;
            }

            long nextAllowedNameChange = plr.Client._Account.LastNameChanged + 60 * 60 * 24 * 30;

            if (nextAllowedNameChange > TCPManager.GetTimeStamp())
            {
                TimeSpan exileSpan = TimeSpan.FromSeconds(nextAllowedNameChange - TCPManager.GetTimeStamp());

                string timeString = (exileSpan.Days > 0 ? exileSpan.Days + " days, " : "") + (exileSpan.Hours > 0 ? exileSpan.Hours + " hours, " : "") + (exileSpan.Minutes > 0 ? exileSpan.Minutes + " minutes." : exileSpan.Seconds + " seconds.");

                plr.SendClientMessage("Your next name change will be permitted in " + timeString, ChatLogFilters.CHATLOGFILTERS_USER_ERROR);

                return true;
            }

            var existingChar = CharMgr.GetCharacter(Player.AsCharacterName(values[0]), false);
            if (existingChar != null)
            {
                plr.SendClientMessage("A player with the name '" + existingChar.Name + "' already exists.", ChatLogFilters.CHATLOGFILTERS_USER_ERROR);
                return true;
            }

            if (values[0].Length < 3)
            {
                plr.SendClientMessage("Player name must be at least 3 characters long.", ChatLogFilters.CHATLOGFILTERS_USER_ERROR);
                return true;
            }

            if (!values[0].All(c => char.IsLetter(c) && c <= 0x7A))
            {
                plr.SendClientMessage("Player names may not contain special characters. This should be completely obvious to you.", ChatLogFilters.CHATLOGFILTERS_USER_ERROR);
                return true;
            }

            if (values[0].Length > 10)
            {
                plr.SendClientMessage("Player names may not be longer then 10 characters long", ChatLogFilters.CHATLOGFILTERS_USER_ERROR);
                return true;
            }

            // Kick straight off for using invalid name.
            if (!CharMgr.AllowName(values[0]))
            {
                PacketOut Out = new PacketOut((byte)Opcodes.F_PLAYER_QUIT, 4);
                Out.WriteHexStringBytes("01000000");
                plr.SendPacket(Out);
                return true;
            }
            //users should not be able to select a new name with a great reoccurrance of the same letter
            ushort duplicate = 0;
            for (int i = 0; i < values[0].Length; i++)
            {
                if (i != 0)
                {
                    if (values[0][i] == values[0][i - 1])
                    {
                        duplicate++;
                    }
                    else
                        duplicate = 0;

                    if (duplicate > 3)
                        break;

                }
            }
            if (duplicate > 3)
            {
                plr.SendClientMessage("You may not have a name with 4 or more of the same letters in a row", ChatLogFilters.CHATLOGFILTERS_USER_ERROR);
                return true;
            }

            plr.Client._Account.LastNameChanged = TCPManager.GetTimeStamp();
            Program.AcctMgr.UpdateAccount(plr.Client._Account);

            string newName = values[0][0].ToString().ToUpper() + values[0].ToLower().Substring(1);
            CharMgr.UpdateCharacterName(plr.Info, newName);

            LogSanction(plr.Info.AccountId, plr, "Name Change", "", $"From {plr.Info.OldName} to {plr.Info.Name}");

            GMCommandLog log = new GMCommandLog
            {
                PlayerName = plr.Info.OldName,
                AccountId = (uint)plr.Client._Account.AccountId,
                Command = "Requested player name change FROM " + plr.Info.OldName + " TO " + plr.Info.Name,
                Date = DateTime.Now
            };
            CharMgr.Database.AddObject(log);

            plr.Name = plr.Info.Name;
            plr.Quit(false, false);

            plr.SendClientMessage(log.Command);
            return true;
        }

        public static bool ToggleQuest(Player plr, ref List<string> values)
        {
            String arg1 = GetString(ref values).ToLower();

            ushort questID = Convert.ToUInt16(arg1);

            Quest q = QuestService.GetQuest(questID);

            if (q != null)
            {
                q.Active = !q.Active;
                plr.SendClientMessage("Quest: \"" + q.Name + "\" is now " + (q.Active ? "Active" : "Inactive"), ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
            }
            else
                plr.SendClientMessage("Quest not found!", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);

            int database = GetInt(ref values);

            if (database > 0)
            {
                WorldMgr.Database.SaveObject(q);

                GMCommandLog log = new GMCommandLog();
                log.PlayerName = plr.Name;
                log.AccountId = (uint)plr.Client._Account.AccountId;
                log.Command = (q.Active ? "ACTIVATED QUEST " : "DEACTIVATED QUEST ") + q.Entry + " " + q.Name;
                log.Date = DateTime.Now;
                CharMgr.Database.AddObject(log);
            }

            return true;
        }

        #endregion

        #region Kick / Ban

        private static Account GetAccountForPlayer(string playerName)
        {
            Player target = Player.GetPlayer(playerName);

            if (target != null && target.Client != null)
                return target.Client._Account;

            Character chara = CharMgr.GetCharacter(playerName, false);

            if (chara == null)
                return null;

            return Program.AcctMgr.GetAccountById(chara.AccountId);
        }

        public static bool GetChar(Player plr, ref List<string> values)
        {
            string charName = GetString(ref values);

            if (string.IsNullOrEmpty(charName))
            {
                plr.SendClientMessage("GETCHAR: Invalid character name.");
                return true;
            }

            var chr = CharMgr.GetCharacter(Player.AsCharacterName(charName), false);

            if (chr == null)
            {
                plr.SendClientMessage($"GETCHAR: No character exists by the name {charName}");
                return true;
            }

            var chars = CharMgr.GetAccountChar(chr.AccountId).Chars
                .Where(e => e != null && e.Value != null)
                .OrderBy(e => e.Realm)
                .ThenBy(e => e.Name).ToList();

            Account account = Program.AcctMgr.GetAccountById(chr.AccountId);
            string result = "Player '" + Player.AsCharacterName(charName) + "' (account='" + account.Username + "') has " + chars.Count + " characters.";

            if (account.GmLevel > 0)
                result += " GM_LEVEL:" + account.GmLevel;
            else if (account.CoreLevel > 0)
                result += " CORE_LEVEL:" + account.CoreLevel;

            result += "\n";

            foreach (var c in chars)
            {
                string faction = c.Realm == 1 ? "ORDER" : "DESTRO";
                string online = "";
                if (c.Value.Online)
                    online = "--ONLINE";
                result += $"  {faction} NAME:{c.Name} LEVEL:{c.Value.Level} RENOWN:{c.Value.RenownRank} CLASS:{((CareerLine)c.CareerLine).ToString().Replace("CAREERLINE_", "")}{online}\n";
            }
            plr.SendClientMessage(result);
            return true;
        }

        public static bool GetOnlineChar(Player plr, ref List<string> values)
        {
            string accountName = GetString(ref values);

            if (string.IsNullOrEmpty(accountName))
            {
                plr.SendClientMessage("GETONLINECHAR: Invalid account name.");
                return true;
            }

            Account acct = Program.AcctMgr.GetAccount(accountName);

            if (acct == null)
            {
                plr.SendClientMessage($"GETONLINECHAR: No account exists by the name {accountName}");
                return true;
            }

            AccountChars chars = CharMgr.GetAccountChar(acct.AccountId);

            for (int i = 0; i < chars.Chars.Length; ++i)
            {
                if (chars.Chars[i] == null || chars.Chars[i].Value == null)
                    continue;
                if (chars.Chars[i].Value.Online)
                {
                    plr.SendClientMessage($"The account {acct.Username} is online on character {chars.Chars[i].Name}.");
                    return true;
                }
            }

            plr.SendClientMessage($"The account {acct.Username} has no online characters.");
            return true;
        }

        public static bool CheckLog(Player plr, ref List<string> values)
        {
            if (values.Count < 1)
            {
                plr.SendClientMessage("CHECKLOG: No name supplied.", ChatLogFilters.CHATLOGFILTERS_USER_ERROR);
                return true;
            }

            string playerName = Player.AsCharacterName(GetString(ref values));

            List<Character> targets = new List<Character>();

            CharMgr.GetCharactersWithName(playerName, targets);

            if (targets.Count == 0)
            {
                plr.SendClientMessage("CHECKLOG: The specified character does not exist.");
                return true;
            }

            plr.SendClientMessage("================");

            foreach (var target in targets)
            {
                Account acct = Program.AcctMgr.GetAccountById(target.AccountId);

                List<AccountSanctionInfo> sanctions = Program.AcctMgr.GetSanctionsFor(target.AccountId).OrderBy(x => x.ActionTime).ToList();

                if (sanctions.Count > 0)
                {
                    plr.SendClientMessage("[Sanction Log]");
                    plr.SendClientMessage(string.IsNullOrEmpty(target.OldName) ? $"Character: {target.Name}" : $"Character: {target.Name} (formerly: {target.OldName})");
                    plr.SendClientMessage($"Account: {acct.Username}");
                    plr.SendClientMessage($"Last IP used: {acct.Ip}");

                    if (acct.Banned == 0)
                        plr.SendClientMessage("Ban status: No Sanction");
                    else if (acct.Banned == 1)
                        plr.SendClientMessage("Ban status: Permanently banned");
                    else if (acct.Banned > TCPManager.GetTimeStamp())
                        plr.SendClientMessage("Ban status: Exiled");
                    else
                        plr.SendClientMessage("Ban status: No Sanction");

                    foreach (var sanction in sanctions)
                        if (!string.IsNullOrEmpty(sanction.ActionDuration))
                            plr.SendClientMessage(string.Format("{0} : {1} for {2} issued by {3} ({4})",
                                new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(sanction.ActionTime).ToString("dd/MM/yyyy"),
                                sanction.ActionType,
                                sanction.ActionDuration,
                                sanction.IssuedBy,
                                sanction.ActionLog));
                        else
                            plr.SendClientMessage(string.Format("{0} : {1} issued by {2} ({3})",
                                new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(sanction.ActionTime).ToString("dd/MM/yyyy"),
                                sanction.ActionType,
                                sanction.IssuedBy,
                                sanction.ActionLog));
                }

                else
                {
                    plr.SendClientMessage("[No Sanctions Logged]");
                    plr.SendClientMessage($"Character: {target.Name}");
                    plr.SendClientMessage($"Account: {acct.Username}");
                }

                plr.SendClientMessage("================");
            }

            return true;
        }

        public static bool Note(Player plr, ref List<string> values)
        {
            string playerName = GetString(ref values);

            string warningReason = GetTotalString(ref values);

            if (string.IsNullOrEmpty(warningReason))
            {
                plr.SendClientMessage("NOTE: No text reason specified.");
                return true;
            }

            Character chara = CharMgr.GetCharacter(playerName, false);

            if (chara == null)
            {
                plr.SendClientMessage("NOTE: The player in question does not exist.");
                return true;
            }

            LogSanction(chara.AccountId, plr, "User Note", "", warningReason);

            return true;
        }

        public static bool Warn(Player plr, ref List<string> values)
        {
            string playerName = GetString(ref values);

            string warningReason = GetTotalString(ref values);

            if (string.IsNullOrEmpty(warningReason))
            {
                plr.SendClientMessage("WARN: No warning reason specified.");
                return true;
            }

            Player target = Player.GetPlayer(playerName);

            if (target != null && target.Client != null)
            {
                target.SendClientMessage("[System] The Game Master " + plr.Client._Account.Username + " has issued you with a warning for the following reason:\n" + warningReason + "\nThis warning will remain on file and may be used to decide the severity of any future punishments.", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
                target.SendClientMessage("WARNING RECEIVED", ChatLogFilters.CHATLOGFILTERS_C_RED_LARGE);
                PacketOut Out = new PacketOut((byte)Opcodes.F_PLAY_SOUND);
                Out.WriteByte(0);
                Out.WriteUInt16(0x0c);
                Out.Fill(0, 10);

                target.SendPacket(Out);

                LogSanction(target.Client._Account.AccountId, plr, "Warning", "", warningReason);

                return true;
            }

            Character chara = CharMgr.GetCharacter(playerName, false);

            if (chara == null)
            {
                plr.SendClientMessage("WARN: The player in question does not exist.");
                return true;
            }

            LogSanction(chara.AccountId, plr, "Warning", "", warningReason);

            Account account = Program.AcctMgr.GetAccountById(chara.AccountId);

            if (account == null)
                return true;

            account.BanReason = "(Warned while offline) " + warningReason;
            account.Banned = 2;

            Program.AcctMgr.UpdateAccount(account);

            return true;
        }

        public static bool BlockAdvice(Player plr, ref List<string> values)
        {
            string playerName = GetString(ref values);

            Player target = Player.GetPlayer(playerName);

            if (target?.Client == null)
            {
                plr.SendClientMessage("BLOCKADVICE: " + playerName + " is not in the game.");
                return true;
            }

            if (target.GmLevel > 0)
            {
                plr.SendClientMessage("BLOCKADVICE: " + playerName + " is a staff member.");
                return true;
            }

            int duration = GetInt(ref values);

            if (duration == 0)
            {
                plr.SendClientMessage("User " + playerName + " is now allowed to use Advice chat.");
                target.SendClientMessage("[System] The Advice channel block against you has been removed.", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);

                target.Client._Account.AdviceBlockEnd = 0;
                Program.AcctMgr.UpdateAccount(target.Client._Account);

                return true;
            }

            int durationMult;

            string lengthTypeString = GetString(ref values).ToLower();

            if (lengthTypeString == "")
            {
                plr.SendClientMessage("BLOCKADVICE: Requires a length modifier (string: months, days, hours, minutes, seconds)");
                return true;
            }

            switch (lengthTypeString)
            {
                case "months":
                    durationMult = 86400 * 30;
                    break;
                case "days":
                    durationMult = 86400;
                    break;
                case "hours":
                    durationMult = 3600;
                    break;
                case "minutes":
                    durationMult = 60;
                    break;
                case "seconds":
                    durationMult = 1;
                    break;
                default:
                    plr.SendClientMessage("Specified duration modifier is invalid.");
                    return true;
            }

            string reasonString = GetTotalString(ref values);

            if (reasonString == "")
            {
                plr.SendClientMessage("BLOCKADVICE: Requires a reason.");
                return true;
            }

            target.Client._Account.AdviceBlockEnd = TCPManager.GetTimeStamp() + duration * durationMult;
            Program.AcctMgr.UpdateAccount(target.Client._Account);

            plr.SendClientMessage("User " + playerName + " is no longer allowed to use Advice chat.");
            LogSanction(target.Client._Account.AccountId, plr, "Advice Block", duration + " " + lengthTypeString, reasonString);
            target.SendClientMessage("You have been blocked from the Advice channel for " + (duration + " " + lengthTypeString) + " by " + plr.Client._Account.Username + "for the following reason:\n" + reasonString + ".\nNext time, read the rules (.rules command).", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);

            return true;

        }

        public static bool Mute(Player plr, ref List<string> values)
        {
            string playerName = GetString(ref values);

            Player target = Player.GetPlayer(playerName);
            //Character character = (Character)CharMgr.Database.SelectObject<Character>("");

            if (target?.Client == null)
            {
                plr.SendClientMessage("HELLMUTE: " + playerName + " is not in the game.");
                return true;
            }

            if (target.GmLevel > 0)
            {
                plr.SendClientMessage("HELLMUTE: " + playerName + " is a staff member.");
                return true;
            }

            int duration = GetInt(ref values);

            if (duration == 0)
            {
                plr.SendClientMessage("User " + playerName + " is no longer hellmuted.");

                target.Client._Account.StealthMuteEnd = 0;
                Program.AcctMgr.UpdateAccount(target.Client._Account);

                return true;
            }

            int durationMult;

            string lengthTypeString = GetString(ref values).ToLower();

            if (lengthTypeString == "")
            {
                plr.SendClientMessage("HELLMUTE: Requires a length modifier (string: months, days, hours, minutes, seconds)");
                return true;
            }

            switch (lengthTypeString)
            {
                case "months":
                case "month":
                    durationMult = 86400 * 30;
                    break;
                case "days":
                case "day":
                    durationMult = 86400;
                    break;
                case "hours":
                case "hour":
                    durationMult = 3600;
                    break;
                case "minutes":
                case "minute":
                    durationMult = 60;
                    break;
                case "seconds":
                case "second":
                    durationMult = 1;
                    break;
                default:
                    plr.SendClientMessage("Specified duration modifier is invalid.");
                    return true;
            }

            string reasonString = GetTotalString(ref values);

            if (reasonString == "")
            {
                plr.SendClientMessage("HELLMUTE: Requires a reason.");
                return true;
            }

            target.Client._Account.StealthMuteEnd = TCPManager.GetTimeStamp() + duration * durationMult;
            Program.AcctMgr.UpdateAccount(target.Client._Account);
            LogSanction(target.Client._Account.AccountId, plr, "Hellmute", duration + " " + lengthTypeString, reasonString);
            plr.SendClientMessage("User " + playerName + " is now hellmuted.");

            GMCommandLog log = new GMCommandLog
            {
                PlayerName = plr.Name,
                AccountId = (uint)plr.Client._Account.AccountId,
                Command = "Muted " + target.Name,
                Date = DateTime.UtcNow
            };
            CharMgr.Database.AddObject(log);

            return true;

        }

        public static bool Eject(Player plr, ref List<string> values)
        {
            string playerName = GetString(ref values);

            Player target = Player.GetPlayer(playerName);

            if (target == null || target.Client == null)
            {
                plr.SendClientMessage("EJECT: " + playerName + " is not in the game.");
                return true;
            }

            if (target.GmLevel > 0 && !Utils.HasFlag(plr.GmLevel, (int)EGmLevel.SourceDev))
            {
                plr.SendClientMessage("EJECT: " + playerName + " is a staff member.");
                return true;
            }

            string reasonString = GetString(ref values);

            if (string.IsNullOrEmpty(reasonString))
            {
                plr.SendClientMessage("EJECT: No reason specified.");
                return true;
            }

            LogSanction(target.Client._Account.AccountId, plr, "Kick", "", reasonString);

            PacketOut Out = new PacketOut((byte)Opcodes.F_PLAYER_QUIT, 4);
            Out.WriteHexStringBytes("01000000");
            target.SendPacket(Out);

            GMCommandLog log = new GMCommandLog
            {
                PlayerName = plr.Name,
                AccountId = (uint)plr.Client._Account.AccountId,
                Command = "EJECT PLAYER " + target.Name,
                Date = DateTime.UtcNow
            };
            CharMgr.Database.AddObject(log);

            return true;
        }

        public static bool Sever(Player plr, ref List<string> values)
        {
            string playerName = GetString(ref values);

            Player target = Player.GetPlayer(playerName);

            if (target == null)
            {
                plr.SendClientMessage("SEVER: " + playerName + " is not in the game.");
                return true;
            }

            if (target.GmLevel > 0 && !Utils.HasFlag(plr.GmLevel, (int)EGmLevel.SourceDev))
            {
                plr.SendClientMessage("SEVER: " + playerName + " is a staff member.");
                return true;
            }

            target.Client?.Disconnect("Connection severance");
            target.Destroy();

            GMCommandLog log = new GMCommandLog
            {
                PlayerName = plr.Name,
                AccountId = (uint)plr.Client._Account.AccountId,
                Command = "SEVER PLAYER " + target.Name,
                Date = DateTime.UtcNow
            };
            CharMgr.Database.AddObject(log);

            return true;

        }

        public static bool Ban(Player plr, ref List<string> values)
        {
            string playerName = GetString(ref values);

            string reasonString = GetTotalString(ref values);

            if (string.IsNullOrEmpty(reasonString))
            {
                plr.SendClientMessage("PERMABAN: No ban/lift reason specified.");
                return true;
            }

            Player target = Player.GetPlayer(playerName);
            Character chara = CharMgr.GetCharacter(playerName, false);

            if (chara == null)
            {
                plr.SendClientMessage("PERMABAN: The player in question does not exist.");
                return true;
            }

            Account account = target?.Client != null ? target.Client._Account : Program.AcctMgr.GetAccountById(chara.AccountId);

            if (account.GmLevel > 1)
            {
                plr.SendClientMessage("BAN: " + playerName + " is a staff member.");
                return true;
            }

            if (account.Banned == 1)
            {
                plr.SendClientMessage("BAN: " + playerName + " is already banned.");
                return true;
            }

            account.Banned = 1;
            account.BanReason = reasonString;
            LogSanction(account.AccountId, plr, "Permanent Ban", "", reasonString);

            Program.AcctMgr.UpdateAccount(account);

            if (target != null)
            {
                PacketOut Out = new PacketOut((byte)Opcodes.F_PLAYER_QUIT, 4);
                Out.WriteHexStringBytes("01000000");
                target.SendPacket(Out);
            }

            GMCommandLog log = new GMCommandLog
            {
                PlayerName = plr.Name,
                AccountId = (uint)plr.Client._Account.AccountId,
                Command = "PERMANENT BAN PLAYER " + playerName,
                Date = DateTime.UtcNow
            };
            CharMgr.Database.AddObject(log);

            lock (Player._Players)
                foreach (Player player in Player._Players)
                    player.SendClientMessage(playerName + " was permanently banned from the server by " + plr.Client._Account.Username + " (" + reasonString + ")", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);

            return true;
        }

        public static bool Exile(Player plr, ref List<string> values)
        {
            string playerName = GetString(ref values);

            Player target = Player.GetPlayer(playerName);

            Account account = GetAccountForPlayer(playerName);

            if (account == null)
            {
                plr.SendClientMessage("EXILE: The specified player does not exist.");
                return true;
            }

            if (account.GmLevel > 1 && !Utils.HasFlag(plr.GmLevel, (int)EGmLevel.SourceDev))
            {
                plr.SendClientMessage("EXILE: " + playerName + " is a staff member.");
                return true;
            }

            int duration = GetInt(ref values);

            if (duration <= 0)
            {
                plr.SendClientMessage("EXILE: A nonzero positive duration is required.");
                return true;
            }

            int durationMult;

            string lengthTypeString = GetString(ref values).ToLower();

            if (lengthTypeString == "")
            {
                plr.SendClientMessage("EXILE: Requires a length modifier (string: months, days, hours, minutes, seconds)");
                return true;
            }

            switch (lengthTypeString)
            {
                case "months":
                case "month":
                    durationMult = 86400 * 30;
                    break;
                case "days":
                case "day":
                    durationMult = 86400;
                    break;
                case "hours":
                case "hour":
                    durationMult = 3600;
                    break;
                case "minutes":
                case "minute":
                    durationMult = 60;
                    break;
                case "seconds":
                case "second":
                    durationMult = 1;
                    break;
                default:
                    plr.SendClientMessage("Specified duration modifier is invalid.");
                    return true;
            }

            string reasonString = GetTotalString(ref values);

            if (reasonString == "")
            {
                plr.SendClientMessage("EXILE: Requires a reason.");
                return true;
            }

            account.Banned = Math.Max(account.Banned, TCPManager.GetTimeStamp() + duration * durationMult);
            account.BanReason = reasonString;
            Program.AcctMgr.UpdateAccount(account);

            LogSanction(account.AccountId, plr, "Exile", duration + " " + lengthTypeString, reasonString);

            Group worldGroup = target.WorldGroup;
            uint groupId = 0;
            if (worldGroup != null)
            {
                lock (worldGroup)
                {
                    if (worldGroup != null)
                    {
                        if (worldGroup._warbandHandler != null)
                        {
                            lock (worldGroup._warbandHandler)
                            {
                                if (worldGroup._warbandHandler != null)
                                {
                                    groupId = worldGroup._warbandHandler.ZeroIndexGroupId;
                                }
                            }
                        }
                        else
                        {
                            groupId = worldGroup.GroupId;
                        }
                    }
                }
            }
            if (groupId != 0)
                Group.EnqueueGroupAction(groupId, new GroupAction(EGroupAction.PlayerLeave, target));

            target?.Teleport(175, 1530613, 106135, 4297, 1700);

            plr.SendClientMessage("You exiled " + playerName + " for " + duration + " " + lengthTypeString + " (expires at time: " + account.Banned + ")");

            target?.SendClientMessage("Your account has been exiled for " + duration + " " + lengthTypeString + " by " + plr.Client._Account.Username + " for the following reason:\n" + reasonString + "\nThis timer will continue to run even if you are offline.", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);

            GMCommandLog log = new GMCommandLog
            {
                PlayerName = plr.Name,
                AccountId = (uint)plr.Client._Account.AccountId,
                Command = "EXILE PLAYER " + playerName,
                Date = DateTime.UtcNow
            };
            CharMgr.Database.AddObject(log);

            return true;
        }

        public static bool Unban(Player plr, ref List<string> values)
        {
            string playerName = GetString(ref values);

            Player target = Player.GetPlayer(playerName);

            Account account = GetAccountForPlayer(playerName);

            if (account == null)
            {
                plr.SendClientMessage("LIFT: The specified player does not exist.");
                return true;
            }

            if (account.Banned == 0)
            {
                plr.SendClientMessage(playerName + " isn't banned.");
                return true;
            }

            string liftString = GetTotalString(ref values);

            if (liftString == "")
            {
                plr.SendClientMessage("LIFT: You must input a reason to lift a ban.");
                return true;
            }

            List<AccountSanctionInfo> sanctions = Program.AcctMgr.GetSanctionsFor(account.AccountId).OrderBy(x => x.ActionTime).ToList();

            for (int i = sanctions.Count - 1; i > 0; --i)
            {
                if (sanctions[i].ActionType == "Lift Ban")
                    break;

                if (sanctions[i].ActionType != "Exile" && sanctions[i].ActionType != "Permanent Ban")
                    continue;

                if (sanctions[i].IssuerGmLevel > plr.Client._Account.GmLevel)
                {
                    plr.SendClientMessage("The staff member who issued this sanction, " + sanctions[i].IssuedBy + ", has a higher GM level (" + sanctions[i].IssuerGmLevel + ") than yours. You cannot repeal this sanction.");
                    return true;
                }

                break;
            }

            plr.SendClientMessage("You removed " + playerName + "'s ban.");

            target?.SendClientMessage("The exile against you has been removed.", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);

            account.Banned = 0;
            account.BanReason = "";
            account.Dirty = true;

            LogSanction(account.AccountId, plr, "Lift Ban", "", liftString);

            Program.AcctMgr.UpdateAccount(account);

            return true;
        }

        private static List<Tuple<uint, string, long>> _annihilationRequests = new List<Tuple<uint, string, long>>();

        public static bool Annihilate(Player developer, ref List<string> values)
        {
            string accountName = GetString(ref values).ToLower();

            if (string.IsNullOrEmpty(accountName))
            {
                developer.SendClientMessage("ANNIHILATE: Bad account name.");
                return true;
            }

            Account account = Program.AcctMgr.GetAccount(accountName);

            if (account == null)
            {
                developer.SendClientMessage("ANNIHILATE: The requested account does not exist.");
                return true;
            }

            if (account.GmLevel > 1)
            {
                developer.SendClientMessage("ANNIHILATE: You cannot wipe a staff account.");
                return true;
            }

            if (account.GmLevel == 0 && account.Banned != 1)
            {
                developer.SendClientMessage("ANNIHILATE: You cannot wipe an active account. Ban it first.");
                return true;
            }

            for (int i = 0; i < _annihilationRequests.Count; ++i)
            {
                if (_annihilationRequests[i].Item1 == developer.Info.CharacterId)
                {
                    if (_annihilationRequests[i].Item2 != accountName)
                    {
                        _annihilationRequests.RemoveAt(i);
                        --i;
                        continue;
                    }

                    long timeToAccept = _annihilationRequests[i].Item3 - TCPManager.GetTimeStampMS();
                    if (timeToAccept > 0)
                    {
                        developer.SendClientMessage("ANNIHILATE: You must wait " + timeToAccept + "ms before you can confirm an annihilation request.");
                        return true;
                    }

                    ProcessAnnihilate(developer, account);
                    _annihilationRequests.RemoveAt(i);
                    return true;
                }
            }

            _annihilationRequests.Add(new Tuple<uint, string, long>(developer.CharacterId, accountName, TCPManager.GetTimeStampMS() + 5000));

            developer.SendClientMessage("ANNIHILATE: You have requested to wipe the account " + accountName + ". Please confirm by reentering the command in 5 seconds.");

            return true;
        }

        private static void ProcessAnnihilate(Player developer, Account account)
        {
            AccountChars acctChars = CharMgr.GetAccountChar(account.AccountId);

            if (acctChars == null || acctChars.Chars.Length == 0)
            {
                developer.SendClientMessage("ANNIHILATE: This account lacks any characters to wipe.");
                return;
            }

            foreach (Character cha in acctChars.Chars)
            {
                if (cha == null)
                    continue;

                if (cha.Value.Online)
                {
                    developer.SendClientMessage("ANNIHILATE: The player is online on " + cha.Name + ". Kick it first.");
                    return;
                }
            }

            foreach (Character cha in acctChars.Chars)
            {
                if (cha == null)
                    continue;

                cha.Surname = "";
                CharMgr.Database.SaveObject(cha);

                cha.Value.Level = 1;
                cha.Value.Xp = 1;
                cha.Value.RestXp = 0;
                cha.Value.Renown = 1;
                cha.Value.RenownRank = 1;
                cha.Value.Money = 0;
                cha.Value.BagBuy = 0;
                cha.Value.BankBuy = 0;
                cha.Value.PlayedTime = 0;
                cha.Value.Morale1 = 0;
                cha.Value.Morale2 = 0;
                cha.Value.Morale3 = 0;
                cha.Value.Morale4 = 0;
                cha.Value.RenownSkills = "";
                cha.Value.MasterySkills = "";
                cha.Value.TitleId = 0;
                cha.Value.GatheringSkill = 0;
                cha.Value.GatheringSkillLevel = 0;
                cha.Value.CraftingSkill = 0;
                cha.Value.CraftingSkillLevel = 0;
                cha.Value.LastSeen = 0;
                cha.Value.Tactic1 = 0;
                cha.Value.Tactic2 = 0;
                cha.Value.Tactic3 = 0;
                cha.Value.Tactic4 = 0;
                cha.Value.RVRKills = 0;
                cha.Value.RVRDeaths = 0;
                cha.Value.CraftingBags = 0;
                CharMgr.Database.SaveObject(cha.Value);

                CharMgr.RemoveItemsFromCharacterId(cha.CharacterId, true);
                CharMgr.RemoveMailFromCharacter(cha);
                CharMgr.RemoveQuestsFromCharacter(cha);
                CharMgr.RemoveToKsFromCharacter(cha);
                CharMgr.RemoveToKKillsFromCharacter(cha);

                developer.SendClientMessage("Wiped " + cha.Name + "...");
            }

            CharMgr.Database.ForceSave();

            LogSanction(account.AccountId, developer, "Annihilate", "", "");
            GMCommandLog log = new GMCommandLog
            {
                PlayerName = developer.Name,
                AccountId = (uint)developer.Client._Account.AccountId,
                Command = "ANNIHILATE ACCOUNT " + account.Username,
                Date = DateTime.UtcNow
            };
            CharMgr.Database.AddObject(log);
            developer.SendClientMessage("Successfully wiped " + account.Username + ".");
        }

        public static bool NoSurname(Player plr, ref List<string> values)
        {
            string playerName = GetString(ref values);

            Player target = Player.GetPlayer(playerName);

            Account account = GetAccountForPlayer(playerName);

            if (account == null)
            {
                plr.SendClientMessage("NOSURNAME: The specified player does not exist.");
                return true;
            }

            if (account.GmLevel > 1 && !Utils.HasFlag(plr.GmLevel, (int)EGmLevel.SourceDev))
            {
                plr.SendClientMessage("NOSURNAME: " + playerName + " is a staff member.");
                return true;
            }

            string reasonString = GetTotalString(ref values);

            if (reasonString == "")
            {
                plr.SendClientMessage("NOSURNAME: Requires a reason.");
                return true;
            }

            account.noSurname = 1;
            Program.AcctMgr.UpdateAccount(account);

            plr.SendClientMessage("You set " + playerName + " to not be able to select a surname");

            target?.SendClientMessage("Your account has been tagged to not be able to set a surname by " + plr.Client._Account.Username + " for the following reason:\n" + reasonString, ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);

            LogSanction(target.Client._Account.AccountId, plr, "No Surname ", "", reasonString);

            GMCommandLog log = new GMCommandLog
            {
                PlayerName = plr.Name,
                AccountId = (uint)plr.Client._Account.AccountId,
                Command = "NOSURNAME PLAYER " + playerName,
                Date = DateTime.UtcNow
            };
            CharMgr.Database.AddObject(log);

            return true;
        }

        public static bool AllowSurname(Player plr, ref List<string> values)
        {
            string playerName = GetString(ref values);

            Player target = Player.GetPlayer(playerName);

            Account account = GetAccountForPlayer(playerName);

            if (account == null)
            {
                plr.SendClientMessage("ALLOWSURNAME: The specified player does not exist.");
                return true;
            }

            if (account.GmLevel > 1 && !Utils.HasFlag(plr.GmLevel, (int)EGmLevel.SourceDev))
            {
                plr.SendClientMessage("ALLOWSURNAME: " + playerName + " is a staff member.");
                return true;
            }

            string reasonString = GetTotalString(ref values);

            if (reasonString == "")
            {
                plr.SendClientMessage("ALLOWSURNAME: Requires a reason.");
                return true;
            }

            account.noSurname = 0;
            Program.AcctMgr.UpdateAccount(account);

            plr.SendClientMessage("You set " + playerName + " to be able to select a surname again");

            target?.SendClientMessage("Your limit to get a surname has been lifted by " + plr.Client._Account.Username + " for the following reason:\n" + reasonString, ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);

            LogSanction(target.Client._Account.AccountId, plr, "Allow Surname ", "", reasonString);

            GMCommandLog log = new GMCommandLog
            {
                PlayerName = plr.Name,
                AccountId = (uint)plr.Client._Account.AccountId,
                Command = "ALLOWSURNAME PLAYER " + playerName,
                Date = DateTime.UtcNow
            };
            CharMgr.Database.AddObject(log);

            return true;
        }

        public static bool ClearSurname(Player plr, ref List<string> values)
        {
            string playerName = GetString(ref values);

            Player target = Player.GetPlayer(playerName);

            Account account = GetAccountForPlayer(playerName);

            if (account == null)
            {
                plr.SendClientMessage("CLEARSURNAME: The specified player does not exist.");
                return true;
            }

            if (account.GmLevel > 1 && !Utils.HasFlag(plr.GmLevel, (int)EGmLevel.SourceDev))
            {
                plr.SendClientMessage("CLEARSURNAME: " + playerName + " is a staff member.");
                return true;
            }

            target.SetLastName("");
            Program.AcctMgr.UpdateAccount(account);

            plr.SendClientMessage("You cleared " + playerName + "s surname");

            target?.SendClientMessage("Your surname has been cleared by " + plr.Client._Account.Username, ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);

            GMCommandLog log = new GMCommandLog
            {
                PlayerName = plr.Name,
                AccountId = (uint)plr.Client._Account.AccountId,
                Command = "CLEARSURNAME " + playerName,
                Date = DateTime.UtcNow
            };
            CharMgr.Database.AddObject(log);

            return true;
        }

        public static bool BlockName(Player plr, ref List<string> values)
        {
            string name = GetString(ref values).ToLower();

            if (name.Length < 4)
            {
                plr.SendClientMessage("BLOCKNAME: You may not filter a string with less than 4 characters.");
                return true;
            }

            char filterType = char.ToUpper(GetString(ref values)[0]);

            if (filterType.Equals('E'))
            {
                if (CharMgr.AddBannedName(name, NameFilterType.Equals))
                    plr.SendClientMessage($"{name} is now prohibited as a character name.");

                else
                    plr.SendClientMessage($"BLOCKNAME: The string {name} is already filtered, either directly or through a more general existing filter.");
                return true;
            }

            if (filterType.Equals('S'))
            {
                if (CharMgr.AddBannedName(name, NameFilterType.StartsWith))
                    plr.SendClientMessage($"Character names beginning with {name} are now prohibited.");
                else
                    plr.SendClientMessage($"BLOCKNAME: The string {name} is already filtered, either directly or through a more general existing filter.");
                return true;
            }

            if (filterType.Equals('C'))
            {
                if (CharMgr.AddBannedName(name, NameFilterType.Contains))
                    plr.SendClientMessage($"Character names containing {name} are now prohibited.");
                else
                    plr.SendClientMessage($"BLOCKNAME: The string {name} is already filtered, either directly or through a more general existing filter.");
                return true;
            }

            plr.SendClientMessage("BLOCKNAME: Invalid filter type. Usage: .blockname <name> <E(quals)|S(tartsWith)|C(ontains)>");

            return true;
        }

        public static bool UnblockName(Player plr, ref List<string> values)
        {
            string name = GetString(ref values).ToLower();

            if (name.Length < 4)
            {
                plr.SendClientMessage("UNBLOCKNAME: You may not filter a string with less than 4 characters.");
                return true;
            }

            if (CharMgr.RemoveBannedName(name))
                plr.SendClientMessage($"Successfully removed the name filter on {name}.");
            else plr.SendClientMessage($"{name} is not filtered.");

            return true;
        }

        public static bool ListBlockedNames(Player plr, ref List<string> values)
        {
            CharMgr.ListBlockedNames(plr);

            return true;
        }

        public static bool RemoveQuests(Player plr, ref List<string> values)
        {
            if (values.Count < 1)
            {
                plr.SendClientMessage("Usage: .removequests playername");
                return true;
            }
            string playerName = GetTotalString(ref values);

            if (string.IsNullOrEmpty(playerName))
            {
                plr.SendClientMessage("REMOVEQUESTS: must specify a player");
                return true;
            }
            Character chara = CharMgr.GetCharacter(playerName, false);

            if (chara == null)
            {
                plr.SendClientMessage("The specified character does not exist");
                return true;
            }
            else
            {
                CharMgr.RemoveQuestsFromCharacter(chara);
                plr.SendClientMessage("Removed all quests from character: " + playerName);
                return true;
            }


        }

        public static bool Hide(Player plr, ref List<string> values)
        {
            if (!GmMgr.GmList.Contains(plr))
            {
                plr.SendClientMessage("You are not on the gmlist currently", ChatLogFilters.CHATLOGFILTERS_USER_ERROR);
                return true;
            }

            else
            {
                GmMgr.NotifyGMOffline(plr);
                plr.SendClientMessage("You have been removed from the gmlist");

                GMCommandLog log = new GMCommandLog
                {
                    PlayerName = plr.Name,
                    AccountId = (uint)plr.Client._Account.AccountId,
                    Command = "Hidden from GMlist",
                    Date = DateTime.Now
                };

                CharMgr.Database.AddObject(log);
                return true;
            }
        }

        #endregion

        #region Events
        public static bool Spooky(Player plr, ref List<string> values)
        {
            /*if (!plr.Spooky)
            {
                var target = plr;

                int modelID = 0;

                if (plr.Realm == Realms.REALMS_REALM_ORDER)
                {
                    if (plr.Info.Race == 1)
                    {
                        if (plr.Info.Sex == 0)
                        {
                            modelID = 1026;
                        }
                        else
                        {
                            modelID = 1027;
                        }
                    }
                    else if (plr.Info.Race == 4)
                    {
                        if (plr.Info.Sex == 0)
                        {
                            modelID = 1035;
                        }
                        else
                        {
                            modelID = 1036;
                        }
                    }
                    else if (plr.Info.Race == 6)
                    {
                        if (plr.Info.Sex == 0)
                        {
                            modelID = 156;
                        }
                        else
                        {
                            modelID = 156;
                        }
                    }

                }
                else
                {
                    if (plr.Info.Race == 2) // Orc
                    {
                        modelID = 1028;
                    }
                    else if (plr.Info.Race == 3) // Goblin
                    {
                        modelID = 1029;
                    }
                    else if (plr.Info.Race == 5) // DE
                    {
                        if (plr.Info.Sex == 0)
                        {
                            modelID = 1037;
                        }
                        else
                        {
                            modelID = 1038;
                        }
                    }

                    else if (plr.Info.Race == 7) // Chaos
                    {

                        if (plr.Info.CareerFlags == 4096)
                        {
                            modelID = 1034;
                        }
                        else if (plr.Info.CareerFlags == 8192)
                        {
                            modelID = 1034;
                        }
                        else if (plr.Info.Sex == 0)
                        {
                            modelID = 156;
                        }
                        else
                        {
                            modelID = 156;
                        }
                    }
                }


                target.ImageNum = (ushort)modelID;

                var Out = new PacketOut((byte)Opcodes.F_PLAYER_IMAGENUM); //F_PLAYER_INVENTORY
                Out.WriteUInt16(target.Oid);
                Out.WriteUInt16((ushort)modelID);
                Out.Fill(0, 18);
                target.DispatchPacket(Out, true);

                plr.Spooky = true;

                Random random = new Random();
                ushort vfx = 0;
                switch (random.Next(1, 3))
                {
                    case 1:
                        vfx = 2498;
                        break;
                    case 2:
                        vfx = 3155;
                        break;
                }

                plr.PlayEffect(vfx);

                var prms = new List<object>() { plr };
                plr.EvtInterface.AddEvent(plr.SpreadSpooky, 120 * 1000, 0, prms);
                plr.SetGearShowing(2, false);

            }*/

            plr.SendClientMessage("Halloween is over!", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);

            return true;
        }

        public static bool NotSpooky(Player plr, ref List<string> values)
        {
            plr.SendClientMessage("Halloween is over!", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
            /*plr.SendClientMessage("You are not affected by spookiness... For now...", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
            plr.SendClientMessage("If you don't want to be affected by spookiness you must run this command when you log in into the game.", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
            plr.Spooky = true;*/
            return true;
        }
        #endregion

        #region Pets
        public static bool Beastmaster(Player plr, ref List<string> values)
        {
            if (plr.Info.CareerLine == 19)
            {
                if (plr.Realm == Realms.REALMS_REALM_ORDER)
                {
                    if (plr.Info.Sex == 0)
                    {
                        plr.Info.ModelId = 39;
                        plr.Model = 39;
                    }
                    else
                    {
                        plr.Info.ModelId = 43;
                        plr.Model = 43;
                    }

                    plr.Info.Realm = (byte)Realms.REALMS_REALM_DESTRUCTION;
                    plr.Info.RealmId = (int)Realms.REALMS_REALM_DESTRUCTION;
                }
                else
                {
                    if (plr.Info.Sex == 0)
                    {
                        plr.Info.ModelId = 46;
                        plr.Model = 46;
                    }
                    else
                    {
                        plr.Info.ModelId = 47;
                        plr.Model = 47;
                    }

                    plr.Info.Realm = (byte)Realms.REALMS_REALM_ORDER;
                    plr.Info.RealmId = (int)Realms.REALMS_REALM_ORDER;
                }
                plr.ForceSave();
                plr.EvtInterface.AddEvent(Kick, 2000, 1, plr);
            }
            else
            {
                plr.SendClientMessage("You are not White Lion, go away...", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
            }
            return true;
        }

        public static bool SetPet(Player plr, ref List<string> values)
        {
            if (plr.Info.CareerLine == 19)
            {
                uint model1 = GetUInt(ref values);
                plr.Info.PetModel = (ushort)model1;
                plr.SaveCharacterInfo();
                plr.ForceSave();
            }
            else
            {
                plr.SendClientMessage("You are not White Lion, go away...", ChatLogFilters.CHATLOGFILTERS_CSR_TELL_RECEIVE);
            }

            return true;
        }

            #endregion
        }
}

