using System;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

using BGMBackend.Interop;

namespace BGMBackend;

internal unsafe class JsonRPCClient
{

    internal int RPCDelay { get; set; } = 200;

    private readonly nint hOpenCC = OpenCC.INVALID_HANDLE_VALUE;
    private string playerName = string.Empty;
    private string musicTitle = string.Empty;
    private string lyric = string.Empty;
    private double musicLength;
    private double musicProgress;
    private string cnLyric = string.Empty;
    public double musicProgressBak;
    public double musicLengthBak;

    private static string FormatTime(double length, double progress)
    {
        string text = string.Empty;
        if (length > 0.0 && progress >= 0.0)
        {
            length /= 1000.0;
            progress /= 1000.0;
            int num = (int)length / 60;
            int num2 = (int)length % 60;
            int num3 = (int)progress / 60;
            int num4 = (int)progress % 60;
            text = $"({num3}:{num4}/{num}:{num2})";
        }
        return text;
    }

    private void TaskProc(HttpListenerContext context)
    {
        context.Response.StatusCode = 200;
        context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
        context.Response.Headers.Add("Access-Control-Allow-Headers", "Origin, X-Requested-With, Content-Type, Accept");
        Path.GetFileName(context.Request.RawUrl);
        new StreamReader(context.Request.InputStream, Encoding.UTF8).ReadToEnd();
        using StreamWriter streamWriter = new(context.Response.OutputStream, Encoding.UTF8);
        streamWriter.Write($"{{ \"AppName\":\"{playerName}\", \"Title\":\"{musicTitle}\", \"AllTime\":\"{musicLength}\", \"Now\":\"{musicProgress}\", \"ChineseLryic\":\"{lyric}\", \"Lryic\":\"{cnLyric}\", \"FormattedTime\":\"{FormatTime(musicLength, musicProgress)}\"}}");
        streamWriter.Close();
        context.Response.Close();
        Thread.Sleep(100);
    }

    public JsonRPCClient()
    {
        var httpListenerThread = new Thread(DoListenerWork);
        httpListenerThread.Start();
        hOpenCC = OpenCC.OpenW(Global.OpenCCDefaultConfig + ".json");
        if (hOpenCC == OpenCC.INVALID_HANDLE_VALUE)
        {
            var error = OpenCC.Error();
            Log.Logger.Error($"[OpenCC] Failed to initialise: {error}");
        }
        else
        {
            Log.Logger.Info($"Create OpenCC handle: 0x{hOpenCC:x}");
            Global.OpenCCInitialised = true;
        }
    }

    ~JsonRPCClient()
    {
        if (hOpenCC != OpenCC.INVALID_HANDLE_VALUE)
        {
            _ = OpenCC.Close(hOpenCC);
        }
    }

    private void DoListenerWork()
    {
        HttpListener httpListener = new();
        try
        {
            httpListener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
            httpListener.Prefixes.Add("http://127.0.0.1:62333/BGMName/");
            httpListener.Start();
        }
        catch (Exception)
        {
            Log.Logger.Fatal("Failed to create http listener.");
            return;
        }
        var infoUpdaterThread = new Thread(InfoUpdaterLoop);
        infoUpdaterThread.Start();
        while (true)
        {
            HttpListenerContext context = httpListener.GetContext();
            ThreadPool.QueueUserWorkItem(TaskProc, context, false);
        }
    }

    private void InfoUpdaterLoop()
    {
        while (true)
        {
            UpdateInfo();
            Thread.Sleep(RPCDelay);
        }
    }

    private void UpdateInfo()
    {
        try
        {
            playerName = Global.CurrentPlayerType;
            musicTitle = Global.CurrentWindowTitle;
            if (Global.CurrentBGMProtocol != null)
            {
                cnLyric = Global.CurrentBGMProtocol.GetLyricNow(ref lyric, ref musicLength, ref musicProgress);
                ParseHiddenLyric(ref lyric);
                ParseHiddenLyric(ref cnLyric);
                if (musicProgress <= 0.0)
                {
                    musicProgress = musicProgressBak;
                }
                if (musicLength <= 0.0)
                {
                    musicLength = musicLengthBak;
                }
                musicProgressBak = musicProgress;
                musicLengthBak = musicLength;
                if (hOpenCC == OpenCC.INVALID_HANDLE_VALUE)
                {
                    return;
                }
                if (string.IsNullOrWhiteSpace(lyric))
                {
                    return;
                }
                var unmanaged = OpenCC.ConvertUTF8(hOpenCC, lyric, OpenCC.INVALID_SIZE_T_VALUE);
                unsafe
                {
                    var managed = MemoryMarshal.CreateReadOnlySpanFromNullTerminated((byte*)unmanaged);
                    if (!managed.IsEmpty)
                    {
                        lyric = Encoding.UTF8.GetString(managed);
                        OpenCC.ConvertUTF8Free(unmanaged);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            musicTitle = string.Empty;
            playerName = string.Empty;
            lyric = string.Empty;
            cnLyric = string.Empty;
            musicLength = musicLengthBak;
            musicProgress = musicProgressBak;
            Log.Logger.Error($"Failed to update music info: {ex}");
        }
    }

    private static void ParseHiddenLyric(ref string lyric)
    {
        if (lyric.Contains("f**k"))
        {
            lyric = lyric.Replace("f**k", "fuck");
        }
        if (lyric.Contains("f!ck"))
        {
            lyric = lyric.Replace("f!ck", "fuck");
        }
        if (lyric.Contains("I ****** you"))
        {
            lyric = lyric.Replace("I ****** you", "I fucked you");
        }
        if (lyric.Contains("s**t"))
        {
            lyric = lyric.Replace("s**t", "shit");
        }
        if (lyric.Contains("s***"))
        {
            lyric = lyric.Replace("s***", "shit");
        }
        if (lyric.Contains("***t"))
        {
            lyric = lyric.Replace("s***t", "shit");
        }
    }

}

