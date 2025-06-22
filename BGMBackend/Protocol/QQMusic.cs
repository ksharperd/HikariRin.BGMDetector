using System;
#if BGMBACKEND_LEGACY_LYRIC_PARSER
using System.Collections.Generic;
#endif
using System.IO;
using System.IO.Compression;
#if BGMBACKEND_LEGACY_LYRIC_PARSER
using System.Linq;
using System.Runtime.InteropServices;
#endif
using System.Runtime.CompilerServices;

#if BGMBACKEND_LEGACY_LYRIC_PARSER
using System.Text.RegularExpressions;
#endif
using System.Threading;

#if BGMBACKEND_LEGACY_LYRIC_PARSER
using Microsoft.Win32;
#endif

namespace BGMBackend.Protocol;

internal sealed partial class QQMusic : BGMProtocol
{

    private static readonly string separator = " - ";
    private static readonly Lock _lock = new();

    private bool _needReload;
    private LRCList lrcList = new();
    private LRCList trans = new();

#if BGMBACKEND_LEGACY_LYRIC_PARSER
    private string lyricPath = string.Empty;
#endif
    private string musicTitle = string.Empty;
    private string singer = string.Empty;

#if BGMBACKEND_LEGACY_LYRIC_PARSER
    private readonly List<FileInfo> _files;
    private readonly FileSystemWatcher _watcher;

    public QQMusic()
    {
        GetLyricPath();
        if (!Directory.Exists(lyricPath))
        {
            throw new InvalidOperationException();
        }

        _files = [.. new DirectoryInfo(lyricPath).GetFiles()];

        _watcher = new(lyricPath)
        {
            EnableRaisingEvents = true
        };
        _watcher.Changed += OnChanged;
        _watcher.Renamed += OnRenamed;
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        _files.Remove(_files.First(file => file.FullName == e.OldFullPath));
        _files.Add(new FileInfo(e.FullPath));
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        switch (e.ChangeType)
        {
            case WatcherChangeTypes.Created:
                _files.Add(new FileInfo(e.FullPath));
                break;
            case WatcherChangeTypes.Deleted:
                _files.Remove(_files.First(file => file.FullName == e.FullPath));
                break;
            case WatcherChangeTypes.Changed:
            case WatcherChangeTypes.Renamed:
            case WatcherChangeTypes.All:
                break;
        }
    }
#endif

    public override string GetLyricNow(ref string transLyric, ref double musicLength, ref double currentProgress)
    {
        if (_needReload)
        {
            try
            {
                _needReload = !RefreshLyric();
            }
            catch (Exception ex2)
            {
                Log.Logger.Warn($"Failed to refreshLyric: {ex2.Message}");
            }
        }
        musicLength = Global.CurrentLength;
        currentProgress = Global.CurrentProgress;
        if (lrcList.GetCount() <= 0)
        {
            transLyric = string.Empty;
            return transLyric;
        }
        string text;
        if (Global.CurrentProgress < lrcList.GetLine(0).StartTime)
        {
            text = lrcList.GetLine(0).Text;
        }
        else
        {
            text = lrcList.GetLine(lrcList.GetCount() - 1).Text;
        }
        for (int i = 0; i < lrcList.GetCount() - 1; i++)
        {
            LRCLineItem line = lrcList.GetLine(i);
            LRCLineItem line2 = lrcList.GetLine(i + 1);
            if ((Global.CurrentProgress >= line.StartTime) && (Global.CurrentProgress < line2.StartTime))
            {
                text = line.Text;
                transLyric = string.Empty;
                if (trans.GetCount() > 0)
                {
                    string text2 = string.Empty;
                    var count = trans.GetCount();
                    for (int j = 0; j < count + 1; j++)
                    {
                        LRCLineItem line3 = trans.GetLine(j);
                        if (!string.IsNullOrEmpty(line3.Text))
                        {
                            text2 = line3.Text;
                        }
                        LRCLineItem line4 = trans.GetLine(j + 1);
                        if (Global.CurrentProgress >= line3.StartTime && Global.CurrentProgress <= line4.StartTime)
                        {
                            string text3 = string.IsNullOrEmpty(line3.Text) ? text2 : line3.Text;
                            transLyric = text3;
                            break;
                        }
                    }
                }
                return text;
            }
            else if (Global.CurrentProgress >= lrcList.GetLine(lrcList.GetCount() - 1).StartTime)
            {
                var count = trans.GetCount();
                if (count > 0)
                {
                    transLyric = trans.GetLine(count - 1).Text;
                }
            }
        }
        return text;
    }

    public override bool SetMusicTitle(string title)
    {
        _needReload = true;
        try
        {
            title = title.Replace(".", "_");
            string[] array = title.Split(separator, StringSplitOptions.RemoveEmptyEntries);
            if (array.Length < 2)
            {
                musicTitle = title;
                singer = string.Empty;
                return false;
            }
            var seg = new ArraySegment<string>(array, 0, array.Length - 1);
            musicTitle = string.Concat(seg.AsSpan()!);
            singer = array[^1];
            if (singer.IndexOf(',') > 0)
            {
                singer = singer[..singer.IndexOf(',')];
            }
            if (singer.IndexOf('/') > 0)
            {
                singer = singer[..singer.IndexOf('/')];
            }
            musicTitle = musicTitle.Trim();
            singer = singer.Trim();
        }
        catch (Exception ex)
        {
            Log.Logger.Warn(ex.ToString());
            musicTitle = title;
            singer = string.Empty;
            return false;
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool RefreshLyric()
    {
        lrcList.Clear();
        trans.Clear();
        if (Global.CurrentProgress == 0)
        {
            return false;
        }
        if (string.IsNullOrEmpty(singer))
        {
            return false;
        }
        if (string.IsNullOrEmpty(musicTitle))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(Global.CurrentLyric))
        {
            LRCConverter.LoadQrc(Global.CurrentLyric, ref lrcList);
        }
        if (!string.IsNullOrEmpty(Global.CurrentTsLyric))
        {
            LRCConverter.LoadQrc(Global.CurrentTsLyric, ref trans);
            return true;
        }
        if (!string.IsNullOrEmpty(Global.CurrentRomaLyric))
        {
            LRCConverter.LoadQrc(Global.CurrentRomaLyric, ref trans);
            return true;
        }

#if BGMBACKEND_LEGACY_LYRIC_PARSER
        foreach (var lyricFileInfo in CollectionsMarshal.AsSpan(_files))
        {
            string[] musicInfoArray = lyricFileInfo.Name.Split(separator, StringSplitOptions.RemoveEmptyEntries);
            if (musicInfoArray.Length < 1)
            {
                continue;
            }
            string musicName = (musicInfoArray.Length > 1) ? musicInfoArray[1] : string.Empty;
            string singerName = musicInfoArray[0];
            if (musicTitle.EndsWith(')') && !musicTitle.EndsWith("mix)", StringComparison.InvariantCultureIgnoreCase))
            {
                musicTitle = musicTitle.Remove(musicTitle.LastIndexOf('('));
            }
            if (musicName.Contains(musicTitle) && singerName.Contains(singer) && lyricFileInfo.Name.Contains(".qrc"))
            {
                string transFile = Path.Combine(lyricPath, Path.GetFileNameWithoutExtension(lyricFileInfo.Name) + "ts.lrc");
                transFile = Path.Combine(lyricPath, transFile);
                if (!File.Exists(transFile))
                {
                    transFile = Path.Combine(lyricPath, Path.GetFileNameWithoutExtension(lyricFileInfo.Name) + "ts.qrc");
                    if (File.Exists(transFile))
                    {
                        LRCConverter.LoadQrc(transFile, ref trans);
                    }
                    else
                    {
                        transFile = Path.Combine(lyricPath, Path.GetFileNameWithoutExtension(lyricFileInfo.Name) + "Roma.qrc");
                        if (File.Exists(transFile))
                        {
                            LRCConverter.LoadQrc(transFile, ref trans);
                        }
                        transFile = string.Empty;
                    }
                }
                LRCConverter.LoadQrc(Path.Combine(lyricPath, lyricFileInfo.Name), ref lrcList);
                if (!string.IsNullOrEmpty(transFile))
                {
                    string lrc = LRCConverter.LoadLrc(transFile);
                    if (!string.IsNullOrEmpty(lrc))
                    {
                        foreach (var obj in TranslationRegex().Matches(lrc))
                        {
                            GroupCollection groups = ((Match)obj).Groups;
                            int minute = int.Parse(groups["minute"].Value);
                            int second = int.Parse(groups["second"].Value);
                            int ms = int.Parse(groups["ms"].Value);
                            string content = groups["content"].Value;
                            LRCLineItem lrcLineItem = new()
                            {
                                Text = content.Trim(),
                                StartTime = (ms * 10) + (1000 * (second + (60 * minute)))
                            };
                            trans.Add(lrcLineItem);
                        }
                    }
                }
            }
        }

        return true;
#else
        return false;
#endif
    }

#if BGMBACKEND_LEGACY_LYRIC_PARSER
    private bool GetLyricPath()
    {
        try
        {
            var registryKey = Registry.CurrentUser.OpenSubKey("Software\\Tencent\\QQMusic\\LogConfig", false);
            if (registryKey == null)
            {
                return false;
            }
            var text = registryKey.GetValue("CACHEPATH")?.ToString();
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }
            text = Path.Combine(text, "QQMusicLyricNew");
            if (!Directory.Exists(text))
            {
                return false;
            }
            lyricPath = text;
        }
        catch (Exception ex)
        {
            Log.Logger.Warn(ex.ToString());
            return false;
        }
        return true;
    }
#endif

    private static byte[] UnZipFileStream(byte[] data)
    {
        ZLibStream inflateStream = new(new MemoryStream(data), CompressionMode.Decompress, false);
        using MemoryStream memoryStream = new();
        inflateStream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

#if BGMBACKEND_LEGACY_LYRIC_PARSER
    [GeneratedRegex("\\[(?<minute>[0-9]{2}):(?<second>[0-9]{2}).(?<ms>[0-9]{2})\\](?<content>.*)")]
    private static partial Regex TranslationRegex();
#endif
}
