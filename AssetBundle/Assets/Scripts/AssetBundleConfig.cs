using System;
using System.Collections.Generic;
using System.Xml.Serialization;

[Serializable]
public class AssetBundleConfig 
{
    [XmlElement("ABList")]
    public List<ABBase> ABList { get; set; }
}


[Serializable]
public class ABBase
{
    [XmlAttribute("ABName")]
    public string ABName { get; set; }
    [XmlAttribute("Path")]
    public string Path { get; set; }
    [XmlAttribute("AssetName")]
    public string AssetName { get; set; }
    [XmlAttribute("Crc")]
    public uint Crc { get; set; }
    [XmlAttribute("ABDependce")]
    public List<string> ABDependce { get; set; }//ab包依赖
}