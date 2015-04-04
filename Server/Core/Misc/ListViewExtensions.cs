﻿using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace xServer.Core.Misc
{
    public static class ListViewExtensions
    {
        private const uint WM_CHANGEUISTATE = 0x127;
        private const uint SET_COLUMN_WIDTH = 4126;
        private const int AUTOSIZE_USEHEADER = -2;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, int pszSubIdList);

        public static void removeDots(ListView TargetListView)
        {
            SendMessage(TargetListView.Handle, WM_CHANGEUISTATE, 65537, 0);
        }

        public static void changeTheme(ListView TargetListView)
        {
            SetWindowTheme(TargetListView.Handle, "Explorer", 0);
        }

        public static void autosizeColumns(ListView TargetListView)
        {
            var lngColumn = 0;

            for (lngColumn = 0; lngColumn <= (TargetListView.Columns.Count - 1); lngColumn++)
            {
                SendMessage(TargetListView.Handle, SET_COLUMN_WIDTH, lngColumn, AUTOSIZE_USEHEADER);
            }
        }
    }
}