using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

using Windows.Win32;

namespace BGMBackend.Protocol;

internal unsafe sealed partial class QQMusic : BGMProtocol
{

    private struct LRCWordItem
    {
        public int Time;

        public string Text;
    }

    private class LRCLineItem
    {
        private int _startTime;
        private int _timeCount;
        private string _text = string.Empty;
        private readonly List<LRCWordItem> _wordItems = [];

        public int StartTime
        {
            get
            {
                return _startTime;
            }
            set
            {
                _startTime = value;
            }
        }

        public int TimeCount
        {
            get
            {
                return _timeCount;
            }
            set
            {
                _timeCount = value;
            }
        }

        public string Text
        {
            get
            {
                return _text;
            }
            set
            {
                _text = value == "//" ? string.Empty : value;
            }
        }

        public int GetCount()
        {
            return _wordItems.Count;
        }

        public LRCWordItem GetItem(int index)
        {
            return _wordItems[index];
        }

        public void SetItem(int index, LRCWordItem value)
        {
            _wordItems[index] = value;
        }

        public void Add(LRCWordItem value)
        {
            _wordItems.Add(value);
        }

        public void Clear()
        {
            _wordItems.Clear();
        }
    }

    private class LRCList
    {
        private readonly List<LRCLineItem> _lines = [];
        private string _actor = string.Empty;
        private string _title = string.Empty;
        private string _editBy = string.Empty;
        private string _totalTime = string.Empty;
        private int _offset;

        public string Actor
        {
            get
            {
                return _actor;
            }
            set
            {
                _actor = value;
            }
        }

        public string Title
        {
            get
            {
                return _title;
            }
            set
            {
                _title = value;
            }
        }

        public string EditBy
        {
            get
            {
                return _editBy;
            }
            set
            {
                _editBy = value;
            }
        }

        public string TotalTime
        {
            get
            {
                return _totalTime;
            }
            set
            {
                _totalTime = value;
            }
        }

        public int Offset
        {
            get
            {
                return _offset;
            }
            set
            {
                _offset = value;
            }
        }

        public int GetCount()
        {
            return _lines.Count;
        }

        public LRCLineItem GetLine(int index)
        {
            return _lines[index];
        }

        public void SetLine(int index, LRCLineItem value)
        {
            _lines[index] = value;
        }

        public void Add(LRCLineItem value)
        {
            _lines.Add(value);
        }

        public void Clear()
        {
            _lines.Clear();
        }
    }

    private static partial class LRCConverter
    {

        private static readonly byte[] QQ_Key1 = "!@#)(NHLiuy*$%^&"u8.ToArray();
        private static readonly byte[] QQ_Key2 = "123ZXC!@#)(*$%^&"u8.ToArray();
        private static readonly byte[] QQ_Key3 = "!@#)(*$%^&abcDEF"u8.ToArray();
        private static readonly byte[] NewQRCHead = "[offset:"u8.ToArray();

        private static string BetweenOf(string str, string subStrLeft, string subStrRight)
        {
            int num = str.IndexOf(subStrLeft) + subStrLeft.Length;
            int num2 = str.IndexOf(subStrRight);
            return str[num..num2];
        }

        private static void ParseLabel(List<string> strList, ref LRCList lrcList)
        {
            foreach (string text in strList)
            {
                if (text.Length >= 2)
                {
                    if (text.Contains("[ar:"))
                    {
                        lrcList.Actor = BetweenOf(text, "ar:", "]");
                    }
                    else if (text.Contains("[ti:"))
                    {
                        lrcList.Title = BetweenOf(text, "ti:", "]");
                    }
                    else if (text.Contains("[by:"))
                    {
                        lrcList.EditBy = BetweenOf(text, "[by:", "]");
                    }
                    else if (text.Contains("[offsetPlayerNow:"))
                    {
                        lrcList.Offset = int.Parse(BetweenOf(text, "offsetPlayerNow:", "]"), NumberStyles.None);
                    }
                    else if (text.Contains("[total:"))
                    {
                        lrcList.TotalTime = BetweenOf(text, "total:", "]");
                    }
                    if (text[0] == '[' && "0123456789".Contains(text[1]))
                    {
                        break;
                    }
                }
            }
        }

        private static void ParseQRCLine(string lineStr, ref LRCList lrcList, string nextLine = "")
        {
            if (string.IsNullOrEmpty(lineStr))
            {
                return;
            }
            if (lineStr[0] == '[' && "0123456789".Contains(lineStr[1]))
            {
                LRCLineItem lrcLineItem = new();
                try
                {
                    int indexOfRightIndicate = lineStr.IndexOf(']');
                    string timeText = lineStr[..(indexOfRightIndicate + 1)];
                    var isNotTransFormat = lineStr.Count(chr => (chr == '(') || chr == ')') >= 2;
                    if (isNotTransFormat)
                    {
                        lrcLineItem.StartTime = int.Parse(BetweenOf(timeText, "[", ","));
                        lrcLineItem.TimeCount = int.Parse(BetweenOf(timeText, ",", "]"));
                    }
                    else
                    {
                        var fixedHour = "00:";
                        var time = TimeSpan.Parse(fixedHour + timeText.Remove(0, 1).Remove(timeText.Length - 2, 1));
                        lrcLineItem.StartTime = (int)time.TotalMilliseconds;
                        if (string.IsNullOrEmpty(nextLine))
                        {
                            lrcLineItem.TimeCount = int.MaxValue;
                        }
                        else
                        {
                            var nextTimeStr = nextLine[..nextLine.IndexOf(']')].Remove(0, 1);
                            var nextLineTime = TimeSpan.Parse(fixedHour + nextTimeStr);
                            lrcLineItem.TimeCount = (int)(nextLineTime - time).TotalMilliseconds;
                        }
                    }
                    lrcLineItem.Text = string.Empty;

                    string lrcText = lineStr[(indexOfRightIndicate + 1)..];
                    if (isNotTransFormat)
                    {
                        int num2 = 0;
                        int i = 0;
                        while (i < lrcText.Length)
                        {
                            if (lrcText[i] == '(' && "0123456789".Contains(lrcText[i + 1]))
                            {
                                LRCWordItem lrcWordItem;
                                lrcWordItem.Text = lrcText[num2..i];
                                LRCLineItem lrcLineItem2 = lrcLineItem;
                                lrcLineItem2.Text += lrcWordItem.Text;
                                num2 = lrcText.IndexOf(')', i + 1) + 1;
                                timeText = lrcText[i..num2];
                                lrcWordItem.Time = int.Parse(BetweenOf(timeText, ",", ")"));
                                i = num2;
                            }
                            i++;
                        }
                    }
                    else
                    {
                        lrcLineItem.Text = lrcText;
                    }
                    lrcList.Add(lrcLineItem);
                }
                catch (Exception ex)
                {
                    Log.Logger.Warn(ex.ToString());
                }
                return;
            }
        }

        private static void DecryptStreamForNewVersion(ref MemoryStream stream)
        {
            byte[] array = new byte[(int)stream.Length];
            nint nint = Marshal.AllocHGlobal((int)stream.Length);
            Marshal.Copy(stream.ToArray(), 0, nint, (int)stream.Length);
            _ = Interop.QQMusic.DecryptData(0, nint, (int)stream.Length);
            Marshal.Copy(nint, array, 0, (int)stream.Length);
            stream = new MemoryStream(array);
        }

        internal static void QRCDecryptStream(ref MemoryStream stream)
        {
            DecryptStreamForNewVersion(ref stream);
            stream.Position = 11L;
            byte[] array = new byte[stream.Length - 11L];
            int num = stream.Read(array, 0, (int)stream.Length - 11);
            nint nint = Marshal.AllocHGlobal((int)stream.Length);
            Marshal.Copy(stream.ToArray(), 0, nint, (int)stream.Length);
            nint nint2 = Marshal.AllocHGlobal(NewQRCHead.Length);
            Marshal.Copy(NewQRCHead, 0, nint2, NewQRCHead.Length);
            nint nint3 = Marshal.AllocHGlobal(QQ_Key1.Length);
            Marshal.Copy(QQ_Key1, 0, nint3, QQ_Key1.Length);
            nint nint4 = Marshal.AllocHGlobal(QQ_Key2.Length);
            Marshal.Copy(QQ_Key2, 0, nint4, QQ_Key2.Length);
            nint nint5 = Marshal.AllocHGlobal(QQ_Key3.Length);
            Marshal.Copy(QQ_Key3, 0, nint5, QQ_Key3.Length);
            var num2 = (int)Native.RtlCompareMemory((void*)nint, (void*)nint2, (nuint)NewQRCHead.Length);
            if (NewQRCHead.Length == num2 && num > 0)
            {
                nint nint6 = Marshal.AllocHGlobal(array.Length);
                Marshal.Copy(array, 0, nint6, array.Length);
                _ = Interop.QQMusic.DDes(nint6, nint3, array.Length);
                _ = Interop.QQMusic.Des(nint6, nint4, array.Length);
                _ = Interop.QQMusic.DDes(nint6, nint5, array.Length);
                Marshal.Copy(nint6, array, 0, array.Length);
                Marshal.FreeHGlobal(nint6);
                byte[] unzippedData = UnZipFileStream(array);
                stream.Dispose();
                stream = new MemoryStream();
                stream.Write(unzippedData, 0, unzippedData.Length);
                stream.Position = 0L;
            }
            Marshal.FreeHGlobal(nint);
            Marshal.FreeHGlobal(nint2);
            Marshal.FreeHGlobal(nint3);
            Marshal.FreeHGlobal(nint4);
            Marshal.FreeHGlobal(nint5);
        }

        internal static string LoadLrc(string fileName)
        {
            FileStream fileStream = new(fileName, FileMode.Open);
            byte[] array = new byte[fileStream.Length];
            fileStream.ReadExactly(array, 0, array.Length);
            fileStream.Close();
            if (array.Length != 0)
            {
                MemoryStream memoryStream = new(array);
                QRCDecryptStream(ref memoryStream);
                return Encoding.UTF8.GetString(memoryStream.ToArray());
            }
            return string.Empty;
        }

        internal static bool LoadQrc(string fileName, ref LRCList lrcList)
        {
            lock (_lock)
            {
                FileStream fileStream = new(fileName, FileMode.Open);
                byte[] array = new byte[fileStream.Length];
                fileStream.ReadExactly(array, 0, array.Length);
                fileStream.Close();
                if (array.Length != 0)
                {
                    MemoryStream memoryStream = new(array);
                    QRCDecryptStream(ref memoryStream);
                    var content = Encoding.UTF8.GetString(memoryStream.ToArray());
                    //Log.Logger.Debug($"Lrc content: {content}");
                    Match match = LyricContentRegex().Match(content);
                    if (match.Success)
                    {
                        List<string> list = [.. match.Value.Split([.. Environment.NewLine])];
                        ParseLabel(list, ref lrcList);
                        foreach (string text in list)
                        {
                            ParseQRCLine(text, ref lrcList);
                        }
                    }
                    else
                    {
                        List<string> list = [.. content.Split([.. Environment.NewLine])];
                        ParseLabel(list, ref lrcList);
                        for (int i = 0; i < list.Count; i++)
                        {
                            ParseQRCLine(list[i], ref lrcList, (i + 1) != list.Count ? list[i + 1] : string.Empty);
                        }
                    }
                    return true;
                }
            }
            return false;
        }

        [GeneratedRegex("LyricContent=\"(?<LyricContent>([\\s\\S]*))\"/>", RegexOptions.IgnoreCase | RegexOptions.Multiline, "en-US")]
        private static partial Regex LyricContentRegex();
    }
}
