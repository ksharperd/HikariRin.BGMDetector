using System;
using System.Collections.Generic;

namespace BGMBackend.Protocol;

internal unsafe sealed partial class CloudMusic : BGMProtocol
{

    public class Lyric(List<Lyric.Item> sentences)
    {
        public List<Item> Sentences = sentences;

        public class Item(int ms, string sentence) : IComparable
        {
            public int MS = ms;

            public string Sentence = sentence;

            public string Trans = string.Empty;

            public int CompareTo(object? obj)
            {
                var item = obj as Item;
                if (MS > item?.MS)
                {
                    return 0;
                }
                if (MS == item?.MS)
                {
                    return 1;
                }
                return -1;
            }
        }
    }

}
