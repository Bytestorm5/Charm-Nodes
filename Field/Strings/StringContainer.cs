﻿using System.IO;
using Field.General;

namespace Field.Strings;

public class StringContainer : Tag
{
    public D2Class_EF998080 Header;
    
    public StringContainer(TagHash hash) : base(hash)
    {
    }


    public string GetStringFromHash(ELanguage language, DestinyHash hash)
    {
        // return Header.StringData[(int)language].ParseStringIndex(Header.StringHashTable.BinarySearch(hash));
        int index = Header.StringHashTable.BinarySearch(hash);
        if (index < 0) 
            return String.Empty;
        return Header.StringData.ParseStringIndex(index);
    }
        
    public Dictionary<DestinyHash, string> GetAllStrings(ELanguage language)
    {
        Dictionary<DestinyHash, string> strings = new Dictionary<DestinyHash, string>();
        for (int i = 0; i < Header.StringHashTable.Count; i++)
        {
            strings.Add(Header.StringHashTable[i], Header.StringData.ParseStringIndex(i));
        }
        return strings;
    }

    protected override void ParseStructs()
    {
        Header = ReadHeader<D2Class_EF998080>();
    }
}