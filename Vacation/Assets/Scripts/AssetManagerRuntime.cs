using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// ���������Ϣ
public class BuildInfos
{
    public int BuildVersion; // �汾��
    public Dictionary<string, ulong> FileNames = new Dictionary<string, ulong>(); // �ļ�
    public ulong FizeTotalSize; // �ļ��д�С
}

public enum AssetBundlePattern
{
    // �༭ģ�������أ�Ӧʹ��AssetDataBase������Դ����
    EditorSimulation,
    // ���ؼ���ģʽ��Ӧ���������·����StreamAssets·���£��Ӹ�·������
    Local,
    // Զ�˼���ģʽ��Ӧ������������Դ��������ַ��Ȼ��ͨ�������������
    // ���ص�ɳ��·��persistentDataPath,�ٽ��м���
    Remote
}

public enum AssetBundleCompressionPattern
{
    // LZMA:����ѹ������С�����ǽ�ѹ�ٶ����������ص�ʱ������������
    LZMA,
    // LZ4:����ѹ���еȣ��ٶȽϿ죬�ٶȸ�None Compression��ࣨ�Ƽ�ʹ�ã�
    LZ4,
    // None Compression:��ѹ������������󣬵��Ǽ���������
    None
}

public enum IncrementalBuildMode
{
    None, // �������Ĭ�Ͼ����������
    UseIncrementalBuild,
    ForceRebuild
}

// Package������¼����Ϣ
public class PackageBuildInfo
{
    public string PackageName;

    public List<AssetBuildInfo> AssetInfos = new List<AssetBuildInfo>();

    public List<string> PackageDependecies = new List<string>(); // ��������
   
    public bool IsSourcePackage = false;  // �����Ƿ��ǳ�ʼ��
}

// Package�е�Assets���֮���¼����Ϣ
public class AssetBuildInfo
{
    // ��Դ���ƣ�����Ҫ������Դ�ǣ�Ӧ�ú͸��ַ�����ͬ
    public string AssetName;

    // ����Դ�����ĸ�AssetBundle
    public string AssetBundleName;
}
public class AssetPackage 
{
    public PackageBuildInfo PackageInfo;

    public string PackageName { get { return PackageInfo.PackageName; } }
    Dictionary<string, UnityEngine.Object> LoadedAssets = new Dictionary<string, UnityEngine.Object>();

    public T LoadAsset<T>(string assetName) where T : UnityEngine.Object
    {
        T assetObject = default;
        foreach(AssetBuildInfo info in PackageInfo.AssetInfos)
        {
            if(info.AssetName == assetName)
            {
                if (LoadedAssets.ContainsKey(assetName))
                {
                    assetObject = LoadedAssets[assetName] as T;
                    return assetObject;
                }

                foreach (string dependAssetName in AssetManagerRuntime.Instance.Manifest.GetAllDependencies(info.AssetBundleName))
                {
                    string dependAssetBundlePath = Path.Combine(AssetManagerRuntime.Instance.AssetBundleLoadPath, dependAssetName);

                    AssetBundle.LoadFromFile(dependAssetBundlePath);
                }

                string assetBundlePath = Path.Combine(AssetManagerRuntime.Instance.AssetBundleLoadPath, info.AssetBundleName);

                AssetBundle bundle = AssetBundle.LoadFromFile(assetBundlePath);
                assetObject = bundle.LoadAsset<T>(assetName);
            }
        }

        if(assetObject == null)
        {
            Debug.LogError($"{assetName}δ��{PackageName}���ҵ�");
        }

        return assetObject;
    }
}


public class AssetManagerRuntime
{
    public static AssetManagerRuntime Instance;  // ��ǰ��ĵ���
    AssetBundlePattern CurrentPattern; // ��Inspector������õĴ��ģʽ
    public string LocalAssetPath; // ���б���Asset������·��
    public string AssetBundleLoadPath; // AssetBundle����·��
    public string DownloadPath;  // ��Դ����·����
    public int LocalAssetVersion;  // ������Դ�汾
    public int RemoteAssetVersion;   // Զ����Դ�汾
    List<string> PackageNames; // �������е�Package��Ϣ

    // ���������Ѽ��ص�Package
    Dictionary<string, AssetPackage> LoadedAssetPackages = new Dictionary<string, AssetPackage>();

    public AssetBundleManifest Manifest;

    
    public static void AssetManagerInit(AssetBundlePattern pattern)
    {
        if(Instance == null)
        {
            Instance = new AssetManagerRuntime();
            Instance.CurrentPattern = pattern;
            Instance.CheckInsperctorBuildPattern();
            Instance.CheckLocalAssetVersion();
            Instance.CheckAssetBundleLoadPath();
        }
    }

    // ��鵱��Inspector����еĴ��ģʽ
    void CheckInsperctorBuildPattern()
    {
        switch (CurrentPattern) 
        {
            case AssetBundlePattern.EditorSimulation:
                break;
            case AssetBundlePattern.Local:
                LocalAssetPath = Path.Combine(Application.streamingAssetsPath, "BuildOutput");
                break;
            case AssetBundlePattern.Remote:
                DownloadPath = Path.Combine(Application.persistentDataPath, "DownloadAssets");
                LocalAssetPath = Path.Combine(Application.persistentDataPath, "BuildOutput");
                break;
        }
    }

    void CheckLocalAssetVersion()
    {
        // asset.version ���������Զ�����չ�����ı��ļ�
        string versionFilePath = Path.Combine(LocalAssetPath, "BuildVersion.version");

        if (!File.Exists(versionFilePath))
        {
            LocalAssetVersion = 100;
            File.WriteAllText(versionFilePath, LocalAssetVersion.ToString());
            return;
        }
        LocalAssetVersion = int.Parse(File.ReadAllText(versionFilePath));
    }

    void CheckAssetBundleLoadPath()
    {
        AssetBundleLoadPath = Path.Combine(LocalAssetPath, LocalAssetVersion.ToString());
    }

    // ����AB��
    public AssetPackage LoadPackage(string packageName)
    {
        string packagePath = null; // ����·��
        string packageString = null; // ���е�����
        if (PackageNames == null)
        {
            packagePath = Path.Combine(AssetBundleLoadPath, "AllPackages");
            packageString = File.ReadAllText(packagePath);

            PackageNames = JsonConvert.DeserializeObject<List<string>>(packageString);
            Debug.Log($"Package����·��Ϊ��{packagePath}");
        }

        if (!PackageNames.Contains(packageName))
        {
            Debug.LogError($"{packageName}���ذ��б��в����ڸð�");
            return null;
        }

        if(Manifest == null)
        {
            string mainBundlePath = Path.Combine(AssetBundleLoadPath, "LocalAssets");
            Debug.Log($"Manifest·��Ϊ��{mainBundlePath}");
            AssetBundle mainBundle = AssetBundle.LoadFromFile(mainBundlePath);
            Manifest = mainBundle.LoadAsset<AssetBundleManifest>(nameof(AssetBundleManifest));
        }

        AssetPackage assetPackage = null;
        if (LoadedAssetPackages.ContainsKey(packageName)) 
        {
            assetPackage = LoadedAssetPackages[packageName];
            Debug.LogWarning($"{packageName}�Ѿ�����");
            return assetPackage;
        }

        assetPackage = new AssetPackage();

        packagePath = Path.Combine(AssetBundleLoadPath, packageName);
        packageString = File.ReadAllText(packagePath);

        Debug.Log($"����·��Ϊ��{packagePath}");
        assetPackage.PackageInfo = JsonConvert.DeserializeObject<PackageBuildInfo>(packageString);
        
        LoadedAssetPackages.Add(assetPackage.PackageName, assetPackage);

        foreach(string dependName in assetPackage.PackageInfo.PackageDependecies)
        {
            LoadPackage(dependName); // ��������
        }
        return assetPackage;
    }

    // ���±��ذ汾
    public void UpdateLocalAssetVersion()
    {
        LocalAssetVersion = RemoteAssetVersion;
        string versionFilePath = Path.Combine(LocalAssetPath, "LocalVersion.version");

        File.WriteAllText(versionFilePath, LocalAssetVersion.ToString());

        CheckAssetBundleLoadPath();

        Debug.Log($"���ظ�����ɣ�{LocalAssetVersion}");
    }
}
