namespace BGMBackend.Protocol;

internal abstract class BGMProtocol
{

    public abstract string GetLyricNow(ref string transLyric, ref double musicLength, ref double currentProgress);

    public abstract bool SetMusicTitle(string title);


}
