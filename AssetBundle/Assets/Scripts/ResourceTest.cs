using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using ALG;
using UnityEngine;

public class ResourceTest : MonoBehaviour
{
    //key = ABname , value = AssetBundle
    private Dictionary<string,AssetBundle> _assetBundles = new Dictionary<string, AssetBundle>();
    private AssetBundleConfig assetBundleConfig = null;
    
    private void Awake()
    {
        TestLoadAB();
    }

    void TestLoadAB()
    {
        assetBundleConfig = LoadAssetBundleConfig();
        string prefabPath = "Assets/Prefabs/Canvas.prefab";
        GameObject obj = Instantiate(LoadAsset<GameObject>(prefabPath));
        obj.name = "NewObj";
    }

    T LoadAsset<T>(string path) where T  : UnityEngine.Object
    {
        string assetName = path.Remove(0, path.LastIndexOf("/")+1);
        ABBase abBase = FindABBase(path);
        AssetBundle assetBundle = LoadAssetBundle(abBase);
        return assetBundle.LoadAsset<T>(assetName);
    }

    AssetBundleConfig LoadAssetBundleConfig()
    {
        string configPath = Application.streamingAssetsPath + "/assetbundleconfig";
        AssetBundle bundleConfig = AssetBundle.LoadFromFile(configPath);
        TextAsset textAsset = bundleConfig.LoadAsset<TextAsset>("AssetBundleConfigByte");
        MemoryStream stream = new MemoryStream(textAsset.bytes);
        BinaryFormatter bf = new BinaryFormatter();
        AssetBundleConfig assetBundleConfig = (AssetBundleConfig) bf.Deserialize(stream);
        stream.Close();
        bundleConfig.Unload(true);
        return assetBundleConfig;
    }
    ABBase FindABBase(string path)
    {
        uint crc = CRC32.GetCRC32(path);
        for (int i = 0; i < assetBundleConfig.ABList.Count; i++)
        {
            if (assetBundleConfig.ABList[i].Crc == crc)
            {
                return assetBundleConfig.ABList[i];
            }
        }
        return null;
    }

    AssetBundle LoadAssetBundle(ABBase abBase)
    {
        if (_assetBundles.ContainsKey(abBase.ABName))
        {
            return _assetBundles[abBase.ABName];
        }
        
        for (int i = 0; i < abBase.ABDependce.Count; i++)
        {
            if (_assetBundles.ContainsKey(abBase.ABDependce[i]))
            {
                continue;
            }
            string bundlePath = Application.streamingAssetsPath + "/" + abBase.ABDependce[i];
            AssetBundle ab = AssetBundle.LoadFromFile(bundlePath);
            _assetBundles.Add(abBase.ABDependce[i],ab);
        }
        AssetBundle assetBundle = AssetBundle.LoadFromFile(Application.streamingAssetsPath + "/" + abBase.ABName);
        _assetBundles.Add(abBase.ABName,assetBundle);
        return assetBundle;
    }
}
