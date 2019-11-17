using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Xml.Serialization;
using ALG;
using UnityEditor;
using UnityEngine;

public class BundleEditor
{
    //Config绝对路径
    private static string ABConfigPath = "Assets/Config/ABConfig.asset";
    //BuildPath
    private static string m_BunleTargetPath = Application.streamingAssetsPath;
    //所有文件夹路径 📁
    private static Dictionary<string,string> m_AllFileDir = new Dictionary<string, string>();
    //过滤的List
    private static List<string> m_AllFileAB = new List<string>();
    //单个prefab的ab包
    private static Dictionary<string,List<string>> m_AllPrefabDir = new Dictionary<string, List<string>>();
    //储存有效路径
    private static List<string> m_ConfigFil = new List<string>();
    
    
    [MenuItem("Tools/BuildBundle")]
    public static void Build()
    {
        m_ConfigFil.Clear();
        m_AllFileAB.Clear();
        m_AllFileDir.Clear();
        m_AllPrefabDir.Clear();
        
        ABConfig abConfig = AssetDatabase.LoadAssetAtPath<ABConfig>(ABConfigPath);
        
        foreach (ABConfig.FileDirABName fileDirAbName in abConfig.m_AllFileDirAB)
        {
            if (m_AllFileDir.ContainsKey(fileDirAbName.ABName))
            {
                Debug.LogError("Ab包名重复");
            }
            else
            {
                m_AllFileDir.Add(fileDirAbName.ABName,fileDirAbName.Path);
                m_AllFileAB.Add(fileDirAbName.Path);
                m_ConfigFil.Add(fileDirAbName.Path);
            }
        }

        string[] allStr = AssetDatabase.FindAssets("t:Prefab", abConfig.m_AllPrefabPath.ToArray());
        for (int i = 0; i < allStr.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(allStr[i]);
            EditorUtility.DisplayProgressBar("查找Prefab","prefab: "+path,i*1f/allStr.Length);
            m_ConfigFil.Add(path);
            if (!ContainAllFileAB(path))
            {
                GameObject obj = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                string[] allDepend = AssetDatabase.GetDependencies(path);
                List<string> allDependPaht = new List<string>();
                for (int j = 0; j < allDepend.Length; j++)
                {
                    if (!ContainAllFileAB(allDepend[j]) && !allDepend[j].EndsWith(".cs"))
                    {
//                        Debug.LogError(allDepend[j]);
                        m_AllFileAB.Add(allDepend[j]);
                        allDependPaht.Add(allDepend[j]);
                    }
                }
                
                if (m_AllPrefabDir.ContainsKey(obj.name))
                {
                    Debug.LogError("存在相同名字： "+obj.name);
                }
                else
                {
                    m_AllPrefabDir.Add(obj.name,allDependPaht);
                }
                
            }
        }

        //给资源设置AB包名
        foreach (string name in m_AllFileDir.Keys)
        {
            SetABName(name,m_AllFileDir[name]);
        }
        foreach (string name in m_AllPrefabDir.Keys)
        {
            SetABName(name,m_AllPrefabDir[name]);
        }
        //开始打包
        BuildAssetBundle();
        //清除ab包名，防止修改meta文件
        string[] oldAbName = AssetDatabase.GetAllAssetBundleNames();
        for (int i = 0; i < oldAbName.Length; i++)
        {
            AssetDatabase.RemoveAssetBundleName(oldAbName[i], true);
            EditorUtility.DisplayProgressBar("清除AB包名",oldAbName[i],i*1f/oldAbName.Length);
        }
        AssetDatabase.Refresh();
        EditorUtility.ClearProgressBar();
    }

    static void BuildAssetBundle()
    {
        string[] allBundles = AssetDatabase.GetAllAssetBundleNames();
        //key = 路径 value = 包名
        Dictionary<string,string> resPathDic = new Dictionary<string, string>();
        for (int i = 0; i < allBundles.Length; i++)
        {
            string[] allBundlePath = AssetDatabase.GetAssetPathsFromAssetBundle(allBundles[i]);
            foreach (string s in allBundlePath)
            {
                if (s.EndsWith(".cs"))
                {
                    continue;
                }
                
                if (ValidPath(s))
                {
                    resPathDic.Add(s,allBundles[i]);
                    Debug.Log("此  " + allBundles[i] + "  包下面包含的资源文件路径： " + s);
                }
                else
                {
                    Debug.LogWarning("此  " + allBundles[i] + "  包下面引用但不会打包的资源文件路径： " + s);
                }
            }
        }
        //删除不用的ab包
        DeletAB();
        //生成配置表
        WriteData(resPathDic);
        BuildPipeline.BuildAssetBundles(m_BunleTargetPath, BuildAssetBundleOptions.ChunkBasedCompression,
            EditorUserBuildSettings.activeBuildTarget);
    }

    static void DeletAB()
    {
        string[] allBundlesName = AssetDatabase.GetAllAssetBundleNames();
        DirectoryInfo direction = new DirectoryInfo(m_BunleTargetPath);
        FileInfo[] files = direction.GetFiles("*", SearchOption.AllDirectories);
        for (int i = 0; i < files.Length; i++)
        {
            if (ConatinABName(files[i].Name,allBundlesName) || files[i].Name.EndsWith(".meta"))
            {
                continue;
            }
            else
            {
//                Debug.Log("此AB包已经被删或改名了,正在删除文件： " + files[i].Name);
                if (File.Exists(files[i].FullName))
                {
                    File.Delete(files[i].FullName);
                }
            }
        }
    }

    static void WriteData(Dictionary<string,string> resPathDic)
    {
        AssetBundleConfig config = new AssetBundleConfig();
        config.ABList = new List<ABBase>();
        foreach (string path in resPathDic.Keys)
        {
            ABBase abBase = new ABBase();
            abBase.Crc = CRC32.GetCRC32(path);
            abBase.Path = path;
            abBase.ABName = resPathDic[path];
            abBase.AssetName = path.Remove(0, path.LastIndexOf("/") + 1);
            abBase.ABDependce = new List<string>();
            string[] resDependce = AssetDatabase.GetDependencies(path);
            for (int i = 0; i < resDependce.Length; i++)
            {
                string tempPath = resDependce[i];
                if (tempPath == path || path.EndsWith(".cs"))
                {
                    continue;
                }

                string abName = "";
                if (resPathDic.TryGetValue(tempPath,out abName))
                {
                    if (abName == resPathDic[path])
                    {
                        continue;
                    }

                    if (!abBase.ABDependce.Contains(abName))
                    {
                        abBase.ABDependce.Add(abName);
                    }
                }
            }
            config.ABList.Add(abBase);
        }
        //写入xml
        string xmlPath = Application.dataPath + "/AssetbundleConfigXML.xml";
        if (File.Exists(xmlPath))
        {
            File.Delete(xmlPath);
        }
        FileStream fileStream = new FileStream(xmlPath,FileMode.Create,FileAccess.ReadWrite,FileShare.ReadWrite);
        StreamWriter streamWriter = new StreamWriter(fileStream,System.Text.Encoding.UTF8);
        XmlSerializer xml = new XmlSerializer(config.GetType());
        xml.Serialize(streamWriter,config);
        streamWriter.Close();
        fileStream.Close();
        //写入二进制
        foreach (ABBase abBase in config.ABList)
        {
            abBase.Path = "";
        }
        string bytePath = Application.dataPath + "/AssetBundleConfig/AssetBundleConfigByte.bytes";
        if (File.Exists(bytePath))
        {
            File.Delete(bytePath);
        }
        fileStream = new FileStream(bytePath,FileMode.Create,FileAccess.ReadWrite,FileShare.ReadWrite);
        BinaryFormatter binaryFormatter = new BinaryFormatter();
        binaryFormatter.Serialize(fileStream,config);
        fileStream.Close();
    }

    //给资源设置AB包名
    static void SetABName(string name, string path)
    {
        AssetImporter assetImporter = AssetImporter.GetAtPath(path);
        if (assetImporter == null)
        {
            Debug.LogError("不存在此路径： "+ path);
        }
        else
        {
            assetImporter.assetBundleName = name;
        }
    }
    static void SetABName(string name, List<string> paths)
    {
        foreach (string path in paths)
        {
            AssetImporter assetImporter = AssetImporter.GetAtPath(path);
            if (assetImporter == null)
            {
                Debug.LogError("不存在此路径： "+ path);
            }
            else
            {
                assetImporter.assetBundleName = name;
            }
        }
    }

    static bool ConatinABName(string name,string[] strs)
    {
        for (int i = 0; i < strs.Length; i++)
        {
            if (name == strs[i])
            {
                return true;
            }
        }
        return false;
    }
    
    //判断单独prefab的路径是否已经被包含到文件夹 📁 路径中了
    static bool ContainAllFileAB(string path)
    {
        for (int i = 0; i < m_AllFileAB.Count; i++)
        {
            if (path == m_AllFileAB[i] || (path.Contains(m_AllFileAB[i]) && path.Replace(m_AllFileAB[i],"")[0] == '/') )
            {
                return true;
            }
        }
        return false;
    }

    //是否为yzxnluj
    static bool ValidPath(string path)
    {
//        return true;
        for (int i = 0; i < m_ConfigFil.Count; i++)
        {
            if (path.Contains(m_ConfigFil[i]))
            {
                return true;
            }
        }
        return false;
    }
}
