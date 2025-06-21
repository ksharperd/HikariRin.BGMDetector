using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace BGMBackend.Protocol;

internal unsafe sealed partial class CloudMusic : BGMProtocol
{
    private const string RSA = "4f95cb2eab0eabf1a124c17046840725ee409cc099c2c93f58fd084c74c64d54563fe6e1265cfe54d45e52b3248d7ccb6b14f247566dd0aee23971e8f3455a46fc28eb633d3265e5206fa3126d97066f02dcbb34d9e048f64ef70eddd43c941d57be2fbd238bfc9d6c7f0ad7a1d76cc347a435fded657e6f2911b2128c4b90a2";

    private static readonly string _historyFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Netease\\CloudMusic\\webdata\\file\\history");
    private static JsonArray? _lastNode;

    private string _musicID = string.Empty;

    private bool _needReload;
    private Lyric? _lyrics;

    public override string GetLyricNow(ref string transLyric, ref double musicLength, ref double currentProgress)
    {
        string empty = string.Empty;
        currentProgress = Global.CurrentProgress;
        musicLength = _needReload ? 0 : Global.CurrentLength;

        if (!TryLoadLyric())
        {
            return empty;
        }

        if (_needReload)
        {
            string? currentTrackLength = _lastNode?[0]?["track"]?["duration"]?.ToString();
            if (currentTrackLength is not null)
            {
                var parseResult = double.TryParse(currentTrackLength, out double result);
                if (parseResult && (result > 0.0))
                {
                    musicLength = result;
                    Global.CurrentLength = (long)(result * 1000);
                }
            }
        }

        if (_lyrics?.Sentences is not List<Lyric.Item> { } sentences)
        {
            return empty;
        }

        var minStartTimeLyric = sentences.Min();
        if ((minStartTimeLyric is not null) && (minStartTimeLyric.MS > currentProgress))
        {
            transLyric = minStartTimeLyric.Trans;
            return minStartTimeLyric.Sentence;
        }

        for (int i = 0; i < sentences.Count - 1; i++)
        {
            var item = sentences[i];
            var item2 = sentences[i + 1];
            if (currentProgress >= item?.MS && currentProgress <= item2?.MS)
            {
                transLyric = item.Trans;
                return item.Sentence;
            }
        }
        if (sentences.Count > 0 && sentences.Last() is not null && !string.IsNullOrEmpty(sentences.Last().Sentence))
        {
            transLyric = sentences.Last().Trans;
            return sentences.Last().Sentence;
        }

        return empty;
    }

    public override bool SetMusicTitle(string title)
    {
        _needReload = true;
        return true;
    }

    private bool TryLoadLyric()
    {
        if (!File.Exists(_historyFilePath))
        {
            return false;
        }
        JsonArray? jsonArray;
        try
        {
            if (_needReload)
            {
                _lastNode = JsonNode.Parse(File.ReadAllText(_historyFilePath))?.AsArray();
            }
            jsonArray = JsonNode.Parse(File.ReadAllText(_historyFilePath))?.AsArray();

            if (jsonArray is null)
            {
                return false;
            }
        }
        catch (Exception)
        {
            return false;
        }
        string? currentTrackId = jsonArray![0]?["track"]?["id"]?.ToString();
        if (currentTrackId is null)
        {
            return false;
        }

        if (!_needReload)
        {
            return _lyrics is not null;
        }
        if (_lyrics?.Sentences is List<Lyric.Item> { } sentences)
        {
            sentences.Clear();
        }
        _lyrics = null;

        _musicID = currentTrackId;
        string? lyricJson = GetLyric(_musicID);
        if (string.IsNullOrEmpty(lyricJson))
        {
            return false;
        }

        _lyrics = GenerateLyricByJson(lyricJson);
        if (_lyrics is null)
        {
            return false;
        }
        else
        {
            _needReload = false;
            return true;
        }
    }

    private static Lyric GenerateLyricByJson(string jsonString)
    {
        List<Lyric.Item> list = [];
        try
        {
            ArgumentException.ThrowIfNullOrEmpty(jsonString);

            var jsonNode = JsonNode.Parse(jsonString);
            ArgumentNullException.ThrowIfNull(jsonNode);

            var lrcNode = jsonNode["lrc"];
            ArgumentNullException.ThrowIfNull(lrcNode);

            var lyricText = lrcNode["lyric"]?.ToString();
            ArgumentNullException.ThrowIfNull(lyricText);

            bool skipNextBlock = false;
            foreach (Match match01 in LrcRegex1().Matches(lyricText))
            {
                skipNextBlock = true;
                List<int> list2 = [];
                GroupCollection groups = match01.Groups;
                int num = int.Parse(groups["minute"].Value);
                int num2 = int.Parse(groups["second"].Value);
                int num3 = int.Parse(groups["ms"].Value);
                string text2 = groups["content"].Value;
                list2.Add(num3 + (1000 * (num2 + (60 * num))));
                int num4 = 0;
                while (text2.StartsWith('['))
                {
                    foreach (Match match02 in LrcRegex2().Matches(text2))
                    {
                        GroupCollection groups2 = match02.Groups;
                        int num5 = int.Parse(groups2["minute"].Value);
                        int num6 = int.Parse(groups2["second"].Value);
                        int num7 = int.Parse(groups2["ms"].Value);
                        list2.Add(num7 + (1000 * (num6 + (60 * num5))));
                        text2 = groups2["content"].Value;
                    }
                    if (num4++ >= 10)
                    {
                        break;
                    }
                }
                for (int i = list2.Count - 1; i >= 0; i--)
                {
                    int num8 = list2[i];
                    list.Add(new Lyric.Item(num8, text2));
                }
            }
            if (!skipNextBlock)
            {
                foreach (Match match03 in LrcRegex3().Matches(lyricText))
                {
                    skipNextBlock = true;
                    List<int> list3 = [];
                    GroupCollection groups3 = match03.Groups;
                    int num9 = int.Parse(groups3["minute"].Value);
                    int num10 = int.Parse(groups3["second"].Value);
                    int num11 = int.Parse(groups3["ms"].Value);
                    string text3 = groups3["content"].Value;
                    list3.Add((num11 * 10) + (1000 * (num10 + (60 * num9))));
                    int num12 = 0;
                    while (text3.StartsWith('['))
                    {
                        foreach (Match match04 in LrcRegex4().Matches(text3))
                        {
                            GroupCollection groups4 = match04.Groups;
                            int num13 = int.Parse(groups4["minute"].Value);
                            int num14 = int.Parse(groups4["second"].Value);
                            int num15 = int.Parse(groups4["ms"].Value);
                            list3.Add((num15 * 10) + (1000 * (num14 + (60 * num13))));
                            text3 = groups4["content"].Value;
                        }
                        if (num12++ >= 10)
                        {
                            break;
                        }
                    }
                    for (int j = list3.Count - 1; j >= 0; j--)
                    {
                        int num16 = list3[j];
                        list.Add(new Lyric.Item(num16, text3));
                    }
                }
            }
            if (!skipNextBlock)
            {
                foreach (Match match05 in LrcRegex5().Matches(lyricText))
                {
                    skipNextBlock = true;
                    List<int> list4 = [];
                    GroupCollection groups5 = match05.Groups;
                    int num17 = int.Parse(groups5["minute"].Value);
                    int num18 = int.Parse(groups5["second"].Value);
                    string text4 = groups5["content"].Value;
                    list4.Add(1000 * (num18 + (60 * num17)));
                    int num19 = 0;
                    while (text4.StartsWith('['))
                    {
                        foreach (Match match06 in LrcRegex6().Matches(text4))
                        {
                            GroupCollection groups6 = match06.Groups;
                            int num20 = int.Parse(groups6["minute"].Value);
                            int num21 = int.Parse(groups6["second"].Value);
                            list4.Add(1000 * (num21 + (60 * num20)));
                            text4 = groups6["content"].Value;
                        }
                        if (num19++ >= 10)
                        {
                            break;
                        }
                    }
                    for (int k = list4.Count - 1; k >= 0; k--)
                    {
                        int num22 = list4[k];
                        list.Add(new Lyric.Item(num22, text4));
                    }
                }
            }

            var translatedLyricNode = jsonNode["tlyric"];
            ArgumentNullException.ThrowIfNull(translatedLyricNode);

            var translatedLyricText = translatedLyricNode["lyric"]?.ToString();
            ArgumentNullException.ThrowIfNull(translatedLyricText);

            skipNextBlock = false;
            foreach (Match match07 in LrcRegex7().Matches(translatedLyricText))
            {
                skipNextBlock = true;
                List<int> list5 = [];
                GroupCollection groups7 = match07.Groups;
                int num23 = int.Parse(groups7["minute"].Value);
                int num24 = int.Parse(groups7["second"].Value);
                int num25 = int.Parse(groups7["ms"].Value);
                string text6 = groups7["content"].Value;
                list5.Add(num25 + (1000 * (num24 + (60 * num23))));
                int num26 = 0;
                while (text6.StartsWith('['))
                {
                    foreach (Match match08 in LrcRegex8().Matches(text6))
                    {
                        GroupCollection groups8 = match08.Groups;
                        int num27 = int.Parse(groups8["minute"].Value);
                        int num28 = int.Parse(groups8["second"].Value);
                        int num29 = int.Parse(groups8["ms"].Value);
                        list5.Add(num29 + (1000 * (num28 + (60 * num27))));
                        text6 = groups8["content"].Value;
                    }
                    if (num26++ >= 10)
                    {
                        break;
                    }
                }
                for (int l = list5.Count - 1; l >= 0; l--)
                {
                    int num30 = list5[l];
                    bool flag3 = false;
                    foreach (Lyric.Item item in list)
                    {
                        if (item.MS == num30)
                        {
                            flag3 = true;
                            item.Trans = text6;
                            break;
                        }
                    }
                    if (!flag3)
                    {
                        list.Add(new Lyric.Item(num25 + (1000 * (num24 + (60 * num23))), text6));
                    }
                }
            }
            if (!skipNextBlock)
            {
                foreach (Match match09 in LrcRegex9().Matches(translatedLyricText))
                {
                    skipNextBlock = true;
                    List<int> list6 = [];
                    GroupCollection groups9 = match09.Groups;
                    int num31 = int.Parse(groups9["minute"].Value);
                    int num32 = int.Parse(groups9["second"].Value);
                    int num33 = int.Parse(groups9["ms"].Value);
                    string text7 = groups9["content"].Value;
                    list6.Add((num33 * 10) + (1000 * (num32 + (60 * num31))));
                    int num34 = 0;
                    while (text7.StartsWith('['))
                    {
                        foreach (Match match10 in LrcRegexA().Matches(text7))
                        {
                            GroupCollection groups10 = match10.Groups;
                            int num35 = int.Parse(groups10["minute"].Value);
                            int num36 = int.Parse(groups10["second"].Value);
                            int num37 = int.Parse(groups10["ms"].Value);
                            list6.Add((num37 * 10) + (1000 * (num36 + (60 * num35))));
                            text7 = groups10["content"].Value;
                        }
                        if (num34++ >= 10)
                        {
                            break;
                        }
                    }
                    for (int m = list6.Count - 1; m >= 0; m--)
                    {
                        int num38 = list6[m];
                        bool flag4 = false;
                        foreach (Lyric.Item item2 in list)
                        {
                            if (item2.MS == num38)
                            {
                                flag4 = true;
                                item2.Trans = text7;
                                break;
                            }
                        }
                        if (!flag4)
                        {
                            list.Add(new Lyric.Item((num33 * 10) + (1000 * (num32 + (60 * num31))), text7));
                        }
                    }
                }
            }
        }
        catch (Exception)
        {
        }
        list.Sort();
        return new Lyric(list);
    }

    private static string UrlEncode(string str)
    {
        StringBuilder stringBuilder = new();
        byte[] bytes = Encoding.UTF8.GetBytes(str);
        for (int i = 0; i < bytes.Length; i++)
        {
            stringBuilder.Append("%" + Convert.ToString(bytes[i], 16));
        }
        return stringBuilder.ToString();
    }

    private static string PostString(string text)
    {
        return $"params={UrlEncode(text)}&encSecKey={RSA}&type=1";
    }

    private static string EncryptText(string text)
    {
        string key = "0CoJUm6Qyw8W8jud";
        string encryptKey = "QNPFxeXrkkbxMMdk";
        return Encrypt(Encrypt(text, key), encryptKey);
    }

    private static string Encrypt(string toEncrypt, string key)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(key);
        Encoding.UTF8.GetBytes(toEncrypt);
        byte[] bytes2 = Encoding.UTF8.GetBytes(toEncrypt);
        using var AES = Aes.Create();
        AES.Key = bytes;
        AES.Mode = CipherMode.CBC;
        AES.Padding = PaddingMode.PKCS7;
        AES.IV = Encoding.UTF8.GetBytes("0102030405060708");
        byte[] array = AES.CreateEncryptor().TransformFinalBlock(bytes2, 0, bytes2.Length);
        return Convert.ToBase64String(array, 0, array.Length);
    }

    private static string? GetLyric(string id)
    {
        string text = EncryptText("{\"id\":\"" + id + "\",\"lv\":\"-1\",\"tv\":\"-1\"}");
        text = PostString(text);
        string? result;
        try
        {
            result = HttpPostForNetEase("http://music.163.com/weapi/song/lyric?csrf_token=", text);
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex.ToString());
            result = string.Empty;
        }
        return result;
    }

    private static string? HttpPostForNetEase(string uri, string parameters)
    {

        [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = nameof(Create))]
        static extern WebRequest Create(WebRequest @this, Uri requestUri, bool useUriBase);

        HttpWebRequest httpWebRequest = (HttpWebRequest)Create(null!, new Uri(uri), false);
        httpWebRequest.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
        httpWebRequest.Referer = "http://music.163.com/";
        httpWebRequest.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64; rv:103.0) Gecko/20100101 Firefox/103.0";
        httpWebRequest.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
        httpWebRequest.Headers.Add("Accept-Language", "zh-CN,zh;q=0.8,en-US;q=0.5,en;q=0.3");
        httpWebRequest.Method = "POST";
        byte[] bytes = Encoding.ASCII.GetBytes(parameters);
        httpWebRequest.ContentLength = bytes.Length;
        Stream requestStream = httpWebRequest.GetRequestStream();
        requestStream.Write(bytes, 0, bytes.Length);
        requestStream.Close();
        WebResponse response = httpWebRequest.GetResponse();
        if (response is null)
        {
            return null;
        }
        return new StreamReader(response.GetResponseStream()).ReadToEnd();
    }

    [GeneratedRegex("\\[(?<minute>[0-9]{2}):(?<second>[0-9]{2}).(?<ms>[0-9]{2,3})\\](?<content>.*)\\n")]
    private static partial Regex LrcRegex1();
    [GeneratedRegex("\\[(?<minute>[0-9]{2}):(?<second>[0-9]{2}).(?<ms>[0-9]{2,3})\\](?<content>.*)")]
    private static partial Regex LrcRegex2();
    [GeneratedRegex("\\[(?<minute>[0-9]{2}):(?<second>[0-9]{2}).(?<ms>[0-9]{2,3})\\](?<content>.*)\\n")]
    private static partial Regex LrcRegex3();
    [GeneratedRegex("\\[(?<minute>[0-9]{2}):(?<second>[0-9]{2}).(?<ms>[0-9]{2,3})\\](?<content>.*)")]
    private static partial Regex LrcRegex4();
    [GeneratedRegex("\\[(?<minute>[0-9]{2}):(?<second>[0-9]{2})\\](?<content>.*)\\n")]
    private static partial Regex LrcRegex5();
    [GeneratedRegex("\\[(?<minute>[0-9]{2}):(?<second>[0-9]{2})\\](?<content>.*)")]
    private static partial Regex LrcRegex6();
    [GeneratedRegex("\\[(?<minute>[0-9]{2}):(?<second>[0-9]{2}).(?<ms>[0-9]{2,3})\\](?<content>.*)\\n")]
    private static partial Regex LrcRegex7();
    [GeneratedRegex("\\[(?<minute>[0-9]{2}):(?<second>[0-9]{2}).(?<ms>[0-9]{2,3})\\](?<content>.*)")]
    private static partial Regex LrcRegex8();
    [GeneratedRegex("\\[(?<minute>[0-9]{2}):(?<second>[0-9]{2}).(?<ms>[0-9]{2,3})\\](?<content>.*)\\n")]
    private static partial Regex LrcRegex9();
    [GeneratedRegex("\\[(?<minute>[0-9]{2}):(?<second>[0-9]{2}).(?<ms>[0-9]{2,3})\\](?<content>.*)")]
    private static partial Regex LrcRegexA();
}
