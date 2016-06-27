﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows.Forms;
using System.Threading;
using System.Runtime.InteropServices;
using System.Globalization;
namespace Mahou
{
    class KeyHook
    {
        public static IntPtr _hookID = IntPtr.Zero;
        public const int WH_KEYBOARD_LL = 13;
        public const int WM_KEYDOWN = 0x0100;
        public const int WM_KEYUP = 0x0101;
        public static bool shift = false, self = false, afterConversion = false,
                           other = false, bothnotmatch = false, printable = false;
        public static Exception notINany = new Exception("Selected text is not in any of selected layouts(locales/languages) in settings\nor contains characters from other than selected layouts(locales/languages).");

        public static IntPtr SetHook(LowLevelProc proc)
        {
            using (Process currProcess = Process.GetCurrentProcess())
            using (ProcessModule currModule = currProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                    GetModuleHandle(currModule.ModuleName), 0);
            }
        }
        public delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);
        public static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            Keys Key = (Keys)vkCode; //"Key" will further be used instead of "(Keys)vkCode"
            if (Key == Keys.LShiftKey || Key == Keys.RShiftKey ||
                Key == Keys.Shift || Key == Keys.ShiftKey)//Checks if any shift is down
            {
                shift = (wParam == (IntPtr)WM_KEYDOWN) ? true : false;
            }
            if (Key == Keys.RControlKey || Key == Keys.LControlKey ||
                Key == Keys.RMenu || Key == Keys.LMenu ||
                Key == Keys.RWin || Key == Keys.LWin)//Checks if any other modifiers is down
            {
                other = (wParam == (IntPtr)WM_KEYDOWN) ? true : false;
            }
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                if (Key == Keys.CapsLock && MMain.MySetts.SwitchLayoutByCaps && !self)
                {
                    SwitchToAnotherLayout();
                    self = true;
                    //Code below removes CapsLock original action
                    keybd_event((int)Keys.CapsLock, (byte)MapVirtualKey((int)Keys.CapsLock, 0), 1, 0);
                    keybd_event((int)Keys.CapsLock, (byte)MapVirtualKey((int)Keys.CapsLock, 0), 1 | 2, 0);
                    self = false;
                    return (IntPtr)0;
                }
                if (Key == Keys.Space && afterConversion)
                {
                    MMain.c_word.Clear();
                    afterConversion = false;
                }
                if (Key == Keys.Back && !self)// Removes last item from current word when user press Backspace
                {
                    if (MMain.c_word.Count != 0)
                    {
                        MMain.c_word.RemoveAt(MMain.c_word.Count - 1);
                    }
                }
                if (Key == Keys.Enter || Key == Keys.Home || Key == Keys.End ||
                    Key == Keys.Tab || Key == Keys.PageDown || Key == Keys.PageUp ||
                    Key == Keys.Left || Key == Keys.Right || Key == Keys.Down || Key == Keys.Up)//Pressing any of these Keys will empty current word
                {
                    MMain.c_word.Clear();
                }
                if (Key == Keys.Space && !self)
                {
                    if (MMain.MySetts.SpaceBreak)
                    {
                        MMain.c_word.Clear();
                    }
                    else
                    {
                        WriteEveryWhere(" ", new YuKey() { yukey = Keys.Space, upper = false });
                    }
                }
                if (
                    (Key >= Keys.D0 && Key <= Keys.Z) ||
                    Key >= Keys.Oem1 && Key <= Keys.OemBackslash
                    )
                {
                    printable = true;
                }
                else { printable = false; }
                if (printable && !self && !other)
                {
                    uint Cyulocale = Locales.GetCurrentLocale();
                    if (!shift)
                    {
                        WriteEveryWhere((MakeAnother(vkCode, Cyulocale)),
                            new YuKey() { yukey = Key, upper = false });
                    }
                    else
                    {
                        WriteEveryWhere((MakeAnother(vkCode, Cyulocale)).ToUpper(),
                            new YuKey() { yukey = Key, upper = true });
                    }
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }
        #region Functions/Struct
        public static void ConvertSelection()
        {
            Locales.IfLessThan2();
            self = true;
            string ClipStr = "";
            for (int i = 3; i != 0; i--)
            {
                Clipboard.Clear();
                //Without Thread.Sleep() below - Clipboard.GetText() will crash,
                KInputs.MakeInput(new KInputs.INPUT[] { KInputs.AddKey(Keys.ControlKey, true, true), KInputs.AddKey(Keys.Insert, true, true) }, false);
                System.Threading.Thread.Sleep(10);
                KInputs.MakeInput(new KInputs.INPUT[] { KInputs.AddKey(Keys.ControlKey, false, true), KInputs.AddKey(Keys.Insert, false, true) }, false);
                Exception threadEx = null;
                Thread staThread = new Thread(
                    delegate()
                    {
                        try
                        {
                            ClipStr = Clipboard.GetText();
                        }

                        catch (Exception ex)
                        {
                            threadEx = ex;
                            Debug.WriteLine(threadEx.Message);
                        }
                    });
                staThread.SetApartmentState(ApartmentState.STA);
                staThread.Start();
                staThread.Join();
            }
            //This prevents from converting text that alredy exist in Clipboard
            //by pressing Scroll without selected text.
            if (!String.IsNullOrEmpty(ClipStr))
            {
                KInputs.MakeInput(new KInputs.INPUT[] { KInputs.AddKey(Keys.Back, true, true) }, false);
                var result = "";
                do
                {
                    if (MMain.MySetts.locale1uId == MMain.MySetts.locale2uId)
                    {
                        result = ClipStr;
                        break;
                    }
                    result = UltimateUnicodeConverter.InAnother(ClipStr, MMain.MySetts.locale2uId, MMain.MySetts.locale1uId, true);
                    Debug.WriteLine("1/2 =" + MMain.MySetts.locale2uId + "/" + MMain.MySetts.locale1uId);
                    //if errored first time try switching locales
                    if (result == "ERROR")
                    {
                        result = UltimateUnicodeConverter.InAnother(ClipStr, MMain.MySetts.locale1uId, MMain.MySetts.locale2uId, true);
                        Debug.WriteLine("1/2 = " + MMain.MySetts.locale1uId + "/" + MMain.MySetts.locale2uId);
                        Debug.WriteLine(result);
                        //if errored again throw exception
                        if (result == "ERROR")
                        {
                            bothnotmatch = true;
                            throw notINany;
                        }
                        bothnotmatch = false;
                    }
                    if (result != "ERROR")
                    {
                        break;
                    }
                } while (result == "ERROR");
                Debug.WriteLine("+" + result + "+");
                //Fix for multiline duplications
                result = Regex.Replace(result, "\r\\D\n?|\n\\D\r?", "\n");
                Debug.WriteLine("-" + result + "-");
                /* This method is with using clipboard, it is faster than below, but not works sometimes... and newlines are eaten with this method :{
                 * Using SetDataObject() that sets result to clipboard 5 times will 100% will work instead of SetText()
                Clipboard.SetDataObject(result, true, 5, 1);
                KInputs.MakeInput(new KInputs.INPUT[] { KInputs.AddKey(Keys.Insert, true, true) }, true);
                System.Threading.Thread.Sleep(20);
                KInputs.MakeInput(new KInputs.INPUT[] { KInputs.AddKey(Keys.Insert, false, true) }, true);
                 */
                //Stable method, slower than above, more workable.
                KInputs.MakeInput(KInputs.AddString(result, true), false);
                //reselects text
                for (int i = result.Length; i != 0; i--)
                {
                    Debug.WriteLine(self.ToString());
                    KInputs.MakeInput(new KInputs.INPUT[] { KInputs.AddKey(Keys.Left, true, true) }, true);
                }
                Clipboard.Clear();
            }
            self = false;
            MahouForm.HKConvertSelection.Register(); //Restores hotkey ability
        }
        public static void ConvertLast()
        {
            Locales.IfLessThan2();
            YuKey[] YuKeys = MMain.c_word.ToArray();
            if (YuKeys.Length > 0)
            {
                self = true;
                SwitchToAnotherLayout();
                for (int e = YuKeys.Length; e != 0; e--)
                {
                    KInputs.MakeInput(new KInputs.INPUT[] { KInputs.AddKey(Keys.Back, true, true) }, false);
                }
                foreach (YuKey yk in YuKeys)
                {
                    KInputs.MakeInput(new KInputs.INPUT[] { KInputs.AddKey(yk.yukey, true, false) }, yk.upper);
                }
                self = false;
                afterConversion = true;
            }
            MahouForm.HKConvertLast.Register(); //Restores hotkey ability
        }
        private static void SwitchToAnotherLayout()
        {
            var nowLocale = Locales.GetCurrentLocale();
            uint notnowLocale = 0;
            if (MMain.locales != null)
            {
                if (nowLocale == MMain.MySetts.locale1uId)
                {
                    notnowLocale = MMain.MySetts.locale2uId;
                }
                else if (nowLocale == MMain.MySetts.locale2uId)
                {
                    notnowLocale = MMain.MySetts.locale1uId;
                }
                PostMessage(Locales.GetForegroundWindow(), 0x50, 0, notnowLocale);
                Debug.WriteLine(notnowLocale);
            }
        }
        public static string MakeAnother(int vkCode, uint uId)
        {
            StringBuilder sb = new StringBuilder(10);
            var lpkst = new byte[256];
            if (shift)
            {
                lpkst[(int)Keys.ShiftKey] = 0xff;
            }
            int rc = ToUnicodeEx((uint)vkCode, (uint)vkCode, lpkst, sb, sb.Capacity, 0, (IntPtr)uId);
            return sb.ToString();
        }
        public struct YuKey // YuKey is struct of key and it state(upper/lower)
        {
            public Keys yukey;
            public bool upper;
        }
        public static void WriteEveryWhere(string vc, YuKey Yu) //as name says
        {
            Console.WriteLine(vc);
            MMain.c_word.Add(Yu);
        }
        #endregion
        #region DLL imports
        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true, CallingConvention = CallingConvention.Winapi)]
        public static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int extraInfo);
        [DllImport("user32.dll")]
        public static extern short MapVirtualKey(int wCode, int wMapType);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int ToUnicodeEx(
            uint wVirtKey,
            uint wScanCode,
            byte[] lpKeyState,
            StringBuilder pwszBuff,
            int cchBuff,
            uint wFlags,
            IntPtr dwhkl);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook,
           LowLevelProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
           IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool PostMessage(IntPtr hhwnd, uint msg, uint wparam, uint lparam);
        #endregion
    }

}
