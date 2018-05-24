﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.IO;
using System.Windows.Forms;
using System.Threading;
using System.Diagnostics;
using nsHashDictionary;
using MYPHandler;
using NLog;

namespace Launcher
{
    public static class Client
    {
        public static int Version = 1;
        //Server WAREMU is running from.
        public static string LocalServerIP = "127.0.0.1";
        //public static string TestServerIP = "72.218.160.249";
        public static int LocalServerPort = 8000;
        public static int TestServerPort = 8000;
        public static bool Started;

        public static string User;
        public static string authToken;
        public static string Language = "English";

        // TCP
        public static Socket _Socket;

        private static Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        public static void Print(string Message)
        {
            Accueil.Acc.Print(Message);
        }

        public static bool Connect(string ip, int port)
        {
            try
            {
                

                if (_Socket != null)
                    _Socket.Close();

                _Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _logger.Info($"Connecting to Launcher Server {ip}:{port}");
                _Socket.Connect(ip, port);

                int size = sizeof(UInt32);
                UInt32 on = 1;
                UInt32 keepAliveInterval = 10000; //Send a packet once every 10 seconds.
                UInt32 retryInterval = 1000; //If no response, resend every second.
                byte[] inArray = new byte[size * 3];
                Array.Copy(BitConverter.GetBytes(on), 0, inArray, 0, size);
                Array.Copy(BitConverter.GetBytes(keepAliveInterval), 0, inArray, size, size);
                Array.Copy(BitConverter.GetBytes(retryInterval), 0, inArray, size * 2, size);
                _Socket.IOControl(IOControlCode.KeepAliveValues, inArray, null);

                //MessageBox.Show(_Socket.Connected.ToString());
                BeginReceive();
                //MessageBox.Show(_Socket.Connected.ToString());
                SendCheck();
            }
            catch (Exception e)
            {
                MessageBox.Show("Can not connect to : " + ip + ":" + port + "\n" + e.Message);
                return false;
            }

            return true;
        }

        public static void Close()
        {
            try
            {
                if (_Socket != null)
                    _Socket.Close();
              
            }
            catch(Exception)
            {

            }
        }

        public static void UpdateLanguage()
        {
            if (Language.Length <= 0)
                return;

            int LangueId = 1;
            switch (Language)
            {
                case "French":
                    LangueId = 2;
                    break;
                case "English":
                    LangueId = 1;
                    break;
                case "Deutch":
                    LangueId = 3;
                    break;
                case "Italian":
                    LangueId = 4; 
                    break;
                case "Spanish":
                    LangueId = 5;
                    break;
                case "Korean":
                    LangueId = 6;
                    break;
                case "Chinese":
                    LangueId = 7;
                    break;
                case "Japanese":
                    LangueId = 9;
                    break;
                case "Russian":
                    LangueId = 10;
                    break;
            };

            string CurDir = Directory.GetCurrentDirectory();

            try
            {
                Directory.SetCurrentDirectory(CurDir + "\\..\\user\\");

                StreamReader Reader = new StreamReader("UserSettings.xml");
                string line = "";
                string TotalStream = "";
                while ( (line = Reader.ReadLine()) != null)
                {
                    Print(line);
                    int Pos = line.IndexOf("Language id=");
                    if (Pos > 0)
                    {
                        Pos = line.IndexOf("\"")+1;
                        int Pos2 = line.LastIndexOf("\"");
                        line = line.Remove(Pos, Pos2-Pos);
                        line = line.Insert(Pos, "" + LangueId);
                    }

                    TotalStream += line +"\n";
                }
                Reader.Close();

                StreamWriter Writer = new StreamWriter("UserSettings.xml", false);
                Writer.Write(TotalStream);
                Writer.Flush();
                Writer.Close();
            }
            catch (Exception e)
            {
                Print("Writing : " + e);
            }
        }

        public static void UpdateRealms()
        {
            PacketOut Out = new PacketOut((byte)Opcodes.CL_INFO);
            SendTCP(Out);
        }

        #region Sender

        // Buffer en train d'être envoyé
        static byte[] m_tcpSendBuffer = new byte[65000];

        // Liste des packets a sender
        static readonly Queue<byte[]> m_tcpQueue = new Queue<byte[]>(256);

        // True si un send est en cours
        static bool m_sendingTcp;

        // Envoi un packet
        public static void SendTCP(PacketOut packet)
        {
            _logger.Info($"Sending TCP Packet {packet.Opcode}");
            //Fix the packet size
            packet.WritePacketLength();

            //Get the packet buffer
            byte[] buf = packet.GetBuffer(); //packet.WritePacketLength sets the Capacity

            //Send the buffer
            SendTCP(buf);
        }

        public static void SendTCP(byte[] buf)
        {
            if (m_tcpSendBuffer == null)
                return;

            //Check if client is connected
            if (_Socket.Connected)
            {

                try
                {
                    lock (m_tcpQueue)
                    {
                        if (m_sendingTcp)
                        {
                            m_tcpQueue.Enqueue(buf);
                            return;
                        }

                        m_sendingTcp = true;
                    }

                    Buffer.BlockCopy(buf, 0, m_tcpSendBuffer, 0, buf.Length);

                    _Socket.BeginSend(m_tcpSendBuffer, 0, buf.Length, SocketFlags.None, m_asyncTcpCallback, null);
                }
                catch
                {
                    Close();
                }
            }
        }

        static readonly AsyncCallback m_asyncTcpCallback = AsyncTcpSendCallback;

        static void AsyncTcpSendCallback(IAsyncResult ar)
        {
            try
            {
                Queue<byte[]> q = m_tcpQueue;

                int sent = _Socket.EndSend(ar);

                int count = 0;
                byte[] data = m_tcpSendBuffer;

                if (data == null)
                    return;

                lock (q)
                {
                    if (q.Count > 0)
                    {
                        //						Log.WarnFormat("async sent {0} bytes, sending queued packets count: {1}", sent, q.Count);
                        count = CombinePackets(data, q, data.Length);
                    }
                    if (count <= 0)
                    {
                        //						Log.WarnFormat("async sent {0} bytes", sent);
                        m_sendingTcp = false;
                        return;
                    }
                }

                _Socket.BeginSend(data, 0, count, SocketFlags.None, m_asyncTcpCallback, null);

            }
            catch (Exception)
            {
                    Close();
            }
        }

        private static int CombinePackets(byte[] buf, Queue<byte[]> q, int length)
        {
            int i = 0;
            do
            {
                var pak = q.Peek();
                if (i + pak.Length > buf.Length)
                {
                    if (i == 0)
                    {
                        q.Dequeue();
                        continue;
                    }
                    break;
                }

                Buffer.BlockCopy(pak, 0, buf, i, pak.Length);
                i += pak.Length;

                q.Dequeue();
            } while (q.Count > 0);

            return i;
        }

        public static void SendTCPRaw(PacketOut packet)
        {
            SendTCP((byte[])packet.GetBuffer().Clone());
        }

        #endregion

        #region Receiver

        private static readonly AsyncCallback ReceiveCallback = OnReceiveHandler;
        static byte[] _pBuf = new byte[2048];


        private static void OnReceiveHandler(IAsyncResult ar)
        {
            try
            {
                int numBytes = _Socket.EndReceive(ar);
                _logger.Debug($"Recieving {numBytes} bytes");

                if (numBytes > 0)
                {
                    byte[] buffer = _pBuf;
                    int bufferSize = numBytes;

                    PacketIn pack = new PacketIn(buffer, 0, bufferSize);
                    OnReceive(pack);
                    BeginReceive();

                }
                else
                {
                    Close();
                }
            }
            catch (Exception ex)
            {
                _logger.Debug($"Exception : {ex.Message}");
            }
        }

        public static void BeginReceive()
        {
            _logger.Debug($"Socket Connected {_Socket.Connected}");
            
            if (_Socket != null && _Socket.Connected)
            {
                int bufSize = _pBuf.Length;

                if (0 >= bufSize) //Do we have space to receive?
                {
                    Close();
                }
                else
                {
                    _Socket.BeginReceive(_pBuf, 0, bufSize, SocketFlags.None, ReceiveCallback, null);
                }
            }
        }

        #endregion

        public static void OnReceive(PacketIn packet)
        {
            lock (packet)
            {
                
                packet.Size = packet.GetUint32();
                packet.Opcode = packet.GetUint8();
                _logger.Debug($"OnReceive Packet size : {packet.Size} opCode : {packet.Opcode}");

                Handle(packet);
            }
        }

        #region Packet

        public static void Handle(PacketIn packet)
        {
            if(!Enum.IsDefined(typeof(Opcodes),(byte)packet.Opcode))
            {
                Print("Invalid opcode : " + packet.Opcode.ToString("X02"));
                return;
            }

            _logger.Debug($"HandlePacket{packet}");

            switch((Opcodes)packet.Opcode)
            {
                case Opcodes.LCR_CHECK:
                    
                    byte Result = packet.GetUint8();

                    switch((CheckResult)Result)
                    {
                        case CheckResult.LAUNCHER_OK:
                            Start();
                            break;
                        case CheckResult.LAUNCHER_VERSION:
                            string Message = packet.GetString();
                            Print(Message);
                            Close();
                            break;
                        case CheckResult.LAUNCHER_FILE:

                            string File = packet.GetString();
                            byte[] Bt = Encoding.ASCII.GetBytes(File);

                            FileInfo Info = new FileInfo("mythloginserviceconfig.xml");  
                            FileStream Str = Info.Create();
                            Str.Write(Bt, 0, Bt.Length);  // Bt is sent from the server (configs/mythloginserviceconfig.xml) - it overwrites the file on the client side.
                            Str.Close();
                            break;
                    }
                    break;

                case Opcodes.LCR_START:

                    Accueil.Acc.ReceiveStart();

                    byte response = packet.GetUint8();
                    _logger.Debug($"HandlePacket. Response Code : {response}");

                    if (response == 1) //invalud user/pass
                    {
                        _logger.Warn($"Invalid User / Pass");
                        Accueil.Acc.statusStrip1.Items[1].Text = $@"Invalid User / Pass";
                        return;
                    }
                    else if (response == 2) //banned
                    {
                        _logger.Warn($"Account is banned");
                        Accueil.Acc.statusStrip1.Items[1].Text = $@"Account is banned";
                        return;
                    }
                    else if (response == 3) //account not active
                    {
                        _logger.Warn($"Account is not active");
                        Accueil.Acc.statusStrip1.Items[1].Text = $@"Account is not active";
                        return;
                    }
                    else if (response > 3)
                    {
                        _logger.Error($"Unknown Response");
                        Accueil.Acc.statusStrip1.Items[1].Text = $@"Unknown Response";
                        return;
                    }
                    else
                    {
                        authToken = packet.GetString();
                        _logger.Info($"Authentication Token Received : {authToken}");
                        Accueil.Acc.statusStrip1.Items[1].Text = $@"Starting Client..";
                        try
                        {
                            
                            string CurrentDir = Directory.GetCurrentDirectory();
                            patchExe();
                            UpdateWarData();

                            _logger.Info($"Starting Client {CurrentDir}\\WAR.exe");

                            Process Pro = new Process();
                            Pro.StartInfo.FileName = "WAR.exe";
                            Pro.StartInfo.Arguments = " --acctname=" + Convert.ToBase64String(Encoding.ASCII.GetBytes(User)) + " --sesstoken=" + Convert.ToBase64String(Encoding.ASCII.GetBytes(authToken));
                            _logger.Info($"Starting process WAR.exe");
                            Pro.Start();
                            Directory.SetCurrentDirectory(CurrentDir);
                        }
                        catch (Exception e)
                        {
                            _logger.Info($"Failed to start Client {e.ToString()}");
                            Print(e.ToString());
                        }
                    }

                    break;

                case Opcodes.LCR_INFO:
                    {
                        _logger.Info($"Processing LCR_INFO : Number Realms : {packet.GetUint8()} Name : {packet.GetString()} Parsed : {packet.GetParsedString()}");

                        //Accueil.Acc.ClearRealms();
                        //byte RealmsCount = packet.GetUint8();
                        //for (byte i = 0; i < RealmsCount; ++i)
                        //{
                        //    bool Online = packet.GetUint8() > 0;
                        //    string Name = packet.GetString();
                        //    uint OnlinePlayers = packet.GetUint32();
                        //    uint OrderCount = packet.GetUint32();
                        //    uint DestructionCount = packet.GetUint32();

                        //    //Accueil.Acc.AddRealm(Name, Online, OnlinePlayers, OrderCount, DestructionCount);

                        //}
                    }break;
            }
        }

        public static void Start()
        {
            if(Started)
                return;

           Started = true;
        }

        public static void SendCheck()
        {
            _logger.Info("Starting SendCheck (CL_CHECK)");
            PacketOut Out = new PacketOut((byte)Opcodes.CL_CHECK);
            Out.WriteUInt32((uint)Version);

            FileInfo Info = new FileInfo("mythloginserviceconfig.xml");
            if (Info.Exists)
            {
                Out.WriteByte(1);
                Out.WriteUInt64((ulong)Info.Length);
            }
            else
            {
                Out.WriteByte(0);
            }

            SendTCP(Out);
        }
        public static void patchExe()
        {
            _logger.Info("Patching WAR.exe");
            using (Stream stream = new FileStream(Directory.GetCurrentDirectory() + "\\..\\WAR.exe", FileMode.OpenOrCreate))
            {

                int encryptAddress = (0x00957FBE + 3) - 0x00400000;
                stream.Seek(encryptAddress, SeekOrigin.Begin);
                stream.WriteByte(0x01);

                //0x90 == 144
                //0x57 == 87
                //0x8B == 139
                //0xF8 == 248
                //0xEB == 235
                //0x32 == 50


                //0x934b468a ==147.75.70.138

                byte[] decryptPatch1 = { 0x90, 0x90, 0x90, 0x90, 0x57, 0x8B, 0xF8, 0xEB, 0x32 };
                int decryptAddress1 = (0x009580CB) - 0x00400000;
                stream.Seek(decryptAddress1, SeekOrigin.Begin);
                stream.Write(decryptPatch1, 0, 9);

                byte[] decryptPatch2 = { 0x90, 0x90, 0x90, 0x90, 0xEB, 0x08 };
                int decryptAddress2 = (0x0095814B) - 0x00400000;
                stream.Seek(decryptAddress2, SeekOrigin.Begin);
                stream.Write(decryptPatch2, 0, 6);

                //stream.WriteByte(0x01);
            }
            _logger.Info("Done patching WAR.exe");
        }
        public static void UpdateWarData()
        {
            try
            {
                _logger.Info("Updating mythloginserviceconfig.xml and data.myp");
                FileStream fs = new FileStream(Application.StartupPath + "\\mythloginserviceconfig.xml", FileMode.Open, FileAccess.Read);

                Directory.SetCurrentDirectory(Directory.GetCurrentDirectory() + "\\..\\");

                HashDictionary hashDictionary = new HashDictionary();
                hashDictionary.AddHash(0x3FE03665, 0x349E2A8C, "mythloginserviceconfig.xml", 0);
                MYPHandler.MYPHandler mypHandler = new MYPHandler.MYPHandler("data.myp", null, null, hashDictionary);
                mypHandler.GetFileTable();

                FileInArchive theFile = mypHandler.SearchForFile("mythloginserviceconfig.xml");

                if (theFile == null)
                {
                    _logger.Error("Can not find config file in data.myp");
                    return;
                }

                if (File.Exists(Application.StartupPath + "\\mythloginserviceconfig.xml") == false)
                {
                    _logger.Error("Missing file : mythloginserviceconfig.xml");
                    return;
                }

                mypHandler.ReplaceFile(theFile, fs);

                fs.Close();
            }
            catch (Exception e)
            {
                Print(e.ToString());
            }
        }





        #endregion
    }
}
