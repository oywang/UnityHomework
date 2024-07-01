using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;



// ��ΪAssets�µ������ű��ᱻ���뵽AssetmblyCharp.dll�У������Ű�������ȥ(��APK����
// ���Բ�����ʹ������UnityEditor�����ռ��µķ���
public class HelloWorld : MonoBehaviour
{
    // Inspector����еĴ��ģʽ
    public AssetBundlePattern LoadPattern;


    // ���ļ��ŵ�HPS�У���ȡ��·��
    public string HTTPAddress = "http://192.168.8.83:8080/";

    public string DownloadPath;

    public string RemoteVersionPath;
    public string DownloadVersionPath;

    public class AssetBundleVersionDifference
    {
        public List<string> AddtionAssetBundles;  // ���ΰ汾�Ա������ӵ�AssetBundle
        public List<string> ReducesAssetBundels;  // ���ΰ汾�Ա��м��ٵ�AssetBundle
    }

    public class DownloadInfo 
    {
        public List<string> DownloadedFileNames = new List<string>();
    }

    void Start()
    {
        AssetManagerRuntime.AssetManagerInit(LoadPattern);
        if (LoadPattern == AssetBundlePattern.Remote)
        {
            StartCoroutine(GetRemoteVersion());
        }
        else
        {
            LoadAsset();
        }
    }


    BuildInfos RemoteBuildInfo;
    // ��ȡԶ�˶Ա��б�
    IEnumerator GetRemoteVersion()
    {
        #region ��ȡԶ�˰汾��
        string remoteVersionFilePath = Path.Combine(HTTPAddress, "BuildOutput", "BuildVersion.version");

        // ����Web����
        UnityWebRequest request = UnityWebRequest.Get(remoteVersionFilePath);
        request.SendWebRequest();

        while (!request.isDone)
        {
            // ����null����ȴ�һ֡
            yield return null;
        }

        if (!string.IsNullOrEmpty(request.error))
        {
            Debug.LogError(request.error);
            yield break;
        }

        // ����汾��
        int version = int.Parse(request.downloadHandler.text);

        // ʹ�ñ�������Զ�˰汾
        if (AssetManagerRuntime.Instance.LocalAssetVersion == version)
        {
            LoadAsset();
            yield break;
        }

        AssetManagerRuntime.Instance.RemoteAssetVersion = version;

        Debug.Log($"Զ����Դ�汾Ϊ��{version}");
        #endregion

        // Զ�˰汾·�������ذ汾·��
        RemoteVersionPath = Path.Combine(HTTPAddress, "BuildOutput", AssetManagerRuntime.Instance.RemoteAssetVersion.ToString());
        DownloadVersionPath = Path.Combine(AssetManagerRuntime.Instance.DownloadPath, AssetManagerRuntime.Instance.RemoteAssetVersion.ToString());

        if (!Directory.Exists(DownloadVersionPath))
        {
            Directory.CreateDirectory(DownloadVersionPath);
        }
        Debug.Log($"����·����{DownloadVersionPath}");

        #region ��ȡԶ��BuildInfo
        string remoteBuildInfoPath = Path.Combine(HTTPAddress, "BuildOutput", version.ToString(), "BuildInfo");

        // ����Web����
        request = UnityWebRequest.Get(remoteBuildInfoPath);
        request.SendWebRequest();
        while (!request.isDone)
        {
            // ����null����ȴ�һ֡
            yield return null;
        }

        if (!string.IsNullOrEmpty(request.error))
        {
            Debug.LogError(request.error);
            yield break;
        }
        #endregion
        string buildInfoString = request.downloadHandler.text;
        RemoteBuildInfo = JsonConvert.DeserializeObject<BuildInfos>(buildInfoString);

        if (RemoteBuildInfo == null || RemoteBuildInfo.FizeTotalSize <= 0)
        {
            yield break;
        }
        // ���������б�
        CreateDownloadList();
    }
    // ���������б�
    void CreateDownloadList()
    {
        // ���ȶ�ȡ���ص������б�TempDownloadInfo�������{"DownloadedFileNames":["AllPackages"]}
        string downloadInfoPath = Path.Combine(AssetManagerRuntime.Instance.DownloadPath, DownloadInfoFileName);
        if (File.Exists(downloadInfoPath))
        {
            string downloadInfoString = File.ReadAllText(downloadInfoPath);
            CurrentDownloadInfo = JsonConvert.DeserializeObject<DownloadInfo>(downloadInfoString);
        }
        else // �ļ������ڣ����½�һ��
        {
            CurrentDownloadInfo = new DownloadInfo();
        }

        // ���Ȼ���Ҫ����AllPackages�Լ�Packages��������Ҫ�ж�AllPackages�Ƿ��Ѿ�����
        if (CurrentDownloadInfo.DownloadedFileNames.Contains("AllPackages"))
        {
            OnCompleted("AllPackages", "���ڱ��ش���");
        }
        else
        {
            string filePath = Path.Combine(RemoteVersionPath, "AllPackages");
            string savePath = Path.Combine(DownloadVersionPath, "AllPackages");
            Downloader downloader = new Downloader(filePath, savePath, OnCompleted, OnProgress, OnError);
            downloader.StartDownload();
        }
    }

    static AssetBundleVersionDifference ContrastAssetBundleVersion(string[] oldVersion, string[] newVersion)
    {
        AssetBundleVersionDifference difference = new AssetBundleVersionDifference();

        // �Ա�ÿһ���ϰ汾��ab��(hash��)������°汾�����ڸð�������Ϊ��Ҫ���ٵİ�
        foreach (var assetBundle in oldVersion)
        {
            if (!newVersion.Contains(assetBundle))
            {
                difference.ReducesAssetBundels.Add(assetBundle);
            }
        }

        // �Ա�ÿһ���°汾��ab��(hash��)������ϰ汾�����ڸð�������Ϊ��Ҫ�����İ�
        foreach (var assetBundle in newVersion)
        {
            if (!oldVersion.Contains(assetBundle))
            {
                difference.AddtionAssetBundles.Add(assetBundle);
            }
        }
        return difference;
    }

    void CopeDownloadAssetsToLocalPath()
    {
        DirectoryInfo directoryInfo = new DirectoryInfo(DownloadVersionPath);

        string localVersionPath = Path.Combine(AssetManagerRuntime.Instance.LocalAssetPath, AssetManagerRuntime.Instance.RemoteAssetVersion.ToString());

        directoryInfo.MoveTo(localVersionPath);

        string downloadInfoPath = Path.Combine(AssetManagerRuntime.Instance.DownloadPath, DownloadPath, DownloadInfoFileName);

        File.Delete(downloadInfoPath);
    }

    DownloadInfo CurrentDownloadInfo = new DownloadInfo();
    string DownloadInfoFileName = "TempDownloadInfo";
    // ���ʱ�ص�
    void OnCompleted(string fileName, string message)
    {
        // ��������ļ��б���������ļ�����ֱ��ִ��Completed�¼��������������
        if (!CurrentDownloadInfo.DownloadedFileNames.Contains(fileName))
        {
            // ��������ɵ��ļ���ӵ��б���
            CurrentDownloadInfo.DownloadedFileNames.Add(fileName);

            // ��DownloadInfo���浽��������Ŀ¼��
            string downloadInfoString = JsonConvert.SerializeObject(CurrentDownloadInfo);
            string downloadSavePath = Path.Combine(AssetManagerRuntime.Instance.DownloadPath, DownloadInfoFileName);
            File.WriteAllText(downloadSavePath, downloadInfoString);
        }

        switch(fileName)
        {
            case "AllPackages":
                CreatePackagesDownloadList();
                break;
            case "AssetBundleHashs":
                CreateAssetBundleDownloadList();
                break;
        }
        // �����������ļ������ͷ������ļ�������ȣ�������������
        if (CurrentDownloadInfo.DownloadedFileNames.Count == RemoteBuildInfo.FileNames.Count)
        {
            CopeDownloadAssetsToLocalPath();
            AssetManagerRuntime.Instance.UpdateLocalAssetVersion();
            LoadAsset();
        }
        Debug.Log(message);
    }
    void LoadAsset()
    {
        AssetPackage package = AssetManagerRuntime.Instance.LoadPackage("A");
        GameObject obj = package.LoadAsset<GameObject>("Assets/SampleAssets/Cube.prefab");

        Instantiate(obj);
    }
    // ����ʱ�ص�
    void OnProgress(float progress, long currentLength, long totalLength)
    {
        Debug.Log($"���ؽ��ȣ�{progress * 100}%����ǰ���س��ȣ�{currentLength * 1.0f / 1024 / 1024}M,�ļ��ܳ��ȣ�{totalLength * 1.0f / 1024 / 1024}M");

    }

    // ����ʱ�ص�
    void OnError(ErrorCode errorCode, string message)
    {
        Debug.LogError(message);
    }


    void CreatePackagesDownloadList()
    {
        string allPackagesPath = Path.Combine(DownloadVersionPath, "AllPackages");
        string allPackagesString = File.ReadAllText(allPackagesPath);
        Debug.Log($"���ص�AllPackages���ݣ�{allPackagesString}");
        List<string> allPackages = JsonConvert.DeserializeObject<List<string>>(allPackagesString);

        Downloader downloader = null;

        foreach(string packageName in allPackages)
        {
            if (!CurrentDownloadInfo.DownloadedFileNames.Contains(packageName))
            {
                string remotePackagePath = Path.Combine(RemoteVersionPath, packageName);
                string remotePackageSavePath = Path.Combine(DownloadVersionPath, packageName);
                downloader = new Downloader(remotePackagePath, remotePackageSavePath, OnCompleted, OnProgress, OnError);
                downloader.StartDownload();
            }
            else
            {
                OnCompleted(packageName, "�����Ѵ���");
            }
        }

        if (!CurrentDownloadInfo.DownloadedFileNames.Contains("AssetBundleHashes"))
        {
            string remoteHashPath = Path.Combine(RemoteVersionPath, "AssetBundleHashes");
            string remoteHashSavePath = Path.Combine(DownloadVersionPath, "AssetBundleHashes");
            downloader = new Downloader(remoteHashPath, remoteHashSavePath, OnCompleted, OnProgress, OnError);
            downloader.StartDownload();
        }
        else
        {
            OnCompleted("AssetBundleHashs", "�����Ѵ���");
        }
    }

    void CreateAssetBundleDownloadList()
    {
        // ���ر��ȡ·��
        string assetBundleHashsPath = Path.Combine(DownloadVersionPath, "AssetBundleHashs");
        string[] localAssetBundleHashs = null;

        if (File.Exists(assetBundleHashsPath))
        {
            string assetBundleHashsString = File.ReadAllText(assetBundleHashsPath);
            localAssetBundleHashs = JsonConvert.DeserializeObject<string[]>(assetBundleHashsString);
        }
        // ��ȡԶ��hash��
        string remoteHashPath = Path.Combine(DownloadVersionPath, "AssetBundleHashs");
        string remoteHashString = File.ReadAllText(remoteHashPath);
        string[] remoteAssetBundleHashs = JsonConvert.DeserializeObject<string[]>(remoteHashPath);

        // ������Ҫ�������ص�AB��
        List<string> downloadAssetNames = null;
        if(localAssetBundleHashs == null)
        {
            Debug.Log("���ض�ȡʧ�ܣ�ֱ������Զ�˱�");
            downloadAssetNames = remoteAssetBundleHashs.ToList();
        }
        else
        {
            AssetBundleVersionDifference difference = ContrastAssetBundleVersion(localAssetBundleHashs, remoteAssetBundleHashs);
            downloadAssetNames = difference.AddtionAssetBundles;
        }

        // �����������
        downloadAssetNames.Add("LocalAssets");
        Downloader downloader = null;
        foreach(string assetBundleName in downloadAssetNames)
        {
            // ��Ϊhash�б��е��ļ��������ļ���С���ļ������ɵģ����»��߽��л��֣�
            // ����Ҫ���»��ߺ�һλ��ʼ��ȡAssetBundle�ľ�������
            string fileName = assetBundleName;
            if (assetBundleName.Contains("_"))
            {
                // �»������һλ����AssetBundleName
                int startIndex = assetBundleName.IndexOf("_") + 1;
                fileName = assetBundleName.Substring(startIndex);
            }

            if (!CurrentDownloadInfo.DownloadedFileNames.Contains(fileName))
            {
                string fileURL = Path.Combine(RemoteVersionPath, fileName);
                string fileSavePath = Path.Combine(DownloadVersionPath, fileName);
                downloader = new Downloader(fileURL, fileSavePath, OnCompleted, OnProgress, OnError);
                downloader.StartDownload();
            }
            else
            {
                OnCompleted(fileName, "�����Ѵ���");
            }
        }
    }


    IEnumerator GetRemoteAssetBundleHash()
    {
        string remoteHashPath = Path.Combine(HTTPAddress, "BuildOutput",
            AssetManagerRuntime.Instance.RemoteAssetVersion.ToString(), "AssetBundleHashs");

        UnityWebRequest request = UnityWebRequest.Get(remoteHashPath);

        request.SendWebRequest();

        while (!request.isDone)
        {
            // ����null����ȴ�һ֡
            yield return null;
        }

        if (!string.IsNullOrEmpty(request.error))
        {
            Debug.LogError(request.error);
            yield break;
        }

        string hashString = request.downloadHandler.text;
        string hashSavePath = Path.Combine(AssetManagerRuntime.Instance.DownloadPath, AssetManagerRuntime.Instance.RemoteAssetVersion.ToString(), "AssetBundleHashs");

        File.WriteAllText(hashSavePath, hashString);
        Debug.Log($"AssetBundleHashs�б�������ɣ�{hashString}");

        CreateDownloadList();
        yield return null;
    }

    //// ��Զ������AB��
    //IEnumerator DownloadFile(string fileName, Action callBack, bool isSaveFile = true)
    //{
    //    // Զ��AB�����ļ���·��
    //    string assetBundleDownloadPath = Path.Combine(HTTPAddress, fileName);

    //    // HPS�е���ΪSampleAssetBundle����·��http://192.168.8.83:8080/SampleAssetBundle/SampleAssetBundle
    //    Debug.Log($"AB���Ӹ�·�����أ�{assetBundleDownloadPath}");
    //    UnityWebRequest webRequest = UnityWebRequest.Get(assetBundleDownloadPath);

    //    yield return webRequest.SendWebRequest();

    //    while (!webRequest.isDone)
    //    {
    //        Debug.Log(webRequest.downloadedBytes); // �������ֽ���
    //        Debug.Log(webRequest.downloadProgress); // ���ؽ���
    //        yield return new WaitForEndOfFrame();
    //    }

    //    // AB����·��
    //    string fileSavePath = Path.Combine(AssetBundleLoadPath, fileName);

    //    //C:/Users/wyh/AppData/LocalLow/DefaultCompany/Test\DownloadAssetBundle
    //    //                              \SampleAssetBundle\SampleAssetBundle
    //    Debug.Log($"Զ��AB������ĵ�ַΪ��{fileSavePath}");
    //    Debug.Log(webRequest.downloadHandler.data.Length);
    //    if (isSaveFile)
    //    {
    //        yield return SaveFile(fileSavePath, webRequest.downloadHandler.data, callBack);
    //    }
    //    else
    //    {
    //        // ��Ŀ������ж϶����Ƿ�Ϊ��
    //        callBack?.Invoke();
    //    }
    //}

    //// ����AB��
    //IEnumerator DownloadAssetBundle(List<string> fileNames, Action callBack = null)
    //{
    //    foreach (string fileName in fileNames)
    //    {
    //        string assetBundleName = fileName;
    //        if (fileName.Contains("_"))
    //        {
    //            // �»��ߺ�һλ����AssetBundleName
    //            int startIndex = fileName.IndexOf("_") + 1;
    //            assetBundleName = fileName.Substring(startIndex);
    //        }

    //        string assetBundleDownloadPath = Path.Combine(HTTPAddress, "BuildOutput", AssetManagerRuntime.Instance.RemoteAssetVersion.ToString(), assetBundleName);

    //        UnityWebRequest request = UnityWebRequest.Get(assetBundleDownloadPath);

    //        request.SendWebRequest();

    //        while (!request.isDone)
    //        {
    //            // ����null����ȴ�һ֡
    //            yield return null;
    //        }

    //        if (!string.IsNullOrEmpty(request.error))
    //        {
    //            Debug.LogError(request.error);
    //            yield break;
    //        }

    //        string assestBundleSavePath = Path.Combine(AssetManagerRuntime.Instance.DownloadPath, AssetManagerRuntime.Instance.RemoteAssetVersion.ToString(), assetBundleName);
    //        Debug.Log(assestBundleSavePath);
    //        File.WriteAllBytes(assestBundleSavePath, request.downloadHandler.data);

    //        Debug.Log($"AssetBundle�������{assetBundleName}");
    //    }

    //    callBack?.Invoke();
    //    yield return null;
    //}


    //IEnumerator GetRemotePackages()
    //{
    //    string remotePackagePath = Path.Combine(HTTPAddress, "BuildOutput",
    //        AssetManagerRuntime.Instance.RemoteAssetVersion.ToString(), "AllPackages");
    //    Debug.Log(remotePackagePath);
    //    UnityWebRequest request = UnityWebRequest.Get(remotePackagePath);

    //    request.SendWebRequest();

    //    while (!request.isDone)
    //    {
    //        // ����null����ȴ�һ֡
    //        yield return null;
    //    }

    //    if (!string.IsNullOrEmpty(request.error))
    //    {
    //        Debug.LogError(request.error);
    //        yield break;
    //    }

    //    string allPackagesString = request.downloadHandler.text;

    //    string packagesSavePath = Path.Combine(AssetManagerRuntime.Instance.DownloadPath, AssetManagerRuntime.Instance.RemoteAssetVersion.ToString(), "AllPackages");

    //    // ��Զ�˷�������Package�б��浽��������·��
    //    File.WriteAllText(packagesSavePath, allPackagesString);
    //    Debug.Log($"Package��·��Ϊ��{packagesSavePath}");

    //    // AllPackage���ת��ListString,�����ض�Ӧ��������
    //    List<string> packageNames = JsonConvert.DeserializeObject<List<string>>(allPackagesString);

    //    foreach (string packageName in packageNames)
    //    {
    //        remotePackagePath = Path.Combine(HTTPAddress, "BuildOutput", AssetManagerRuntime.Instance.RemoteAssetVersion.ToString(), packageName);

    //        request = UnityWebRequest.Get(remotePackagePath);

    //        request.SendWebRequest();

    //        while (!request.isDone)
    //        {
    //            // ����null����ȴ�һ֡
    //            yield return null;
    //        }

    //        if (!string.IsNullOrEmpty(request.error))
    //        {
    //            Debug.LogError(request.error);
    //            yield break;
    //        }

    //        string packageString = request.downloadHandler.text;

    //        packagesSavePath = Path.Combine(AssetManagerRuntime.Instance.DownloadPath, AssetManagerRuntime.Instance.RemoteAssetVersion.ToString(), packageName);

    //        File.WriteAllText(packagesSavePath, packageString);
    //        Debug.Log($"package������ϣ�{packageName}");
    //    }

    //    StartCoroutine(GetRemoteAssetBundleHash());
    //    yield return null;
    //}

    // ��Զ�̵��ļ�д�뵽����
    //IEnumerator SaveFile(string savePath, byte[] bytes, Action callBack)
    //{
    //    FileStream fileStream = File.Open(savePath, FileMode.OpenOrCreate);

    //    yield return fileStream.WriteAsync(bytes, 0, bytes.Length);

    //    // �ͷ��ļ����������ļ���һֱ���ڶ�ȡ��״̬�����ܱ��������̶�ȡ
    //    fileStream.Flush();
    //    fileStream.Close();
    //    fileStream.Dispose();

    //    callBack?.Invoke();
    //    Debug.Log($"{savePath}�ļ��������");

    //}


    //void CheckAssetBundleLoadPath() // ����������·��
    //{
    //    switch (LoadPattern)
    //    {
    //        case AssetBundlePattern.EditorSimulation:
    //            break;
    //        case AssetBundlePattern.Local:
    //            AssetBundleLoadPath = Path.Combine(Application.streamingAssetsPath);
    //            break;
    //        case AssetBundlePattern.Remote:
    //            // AB��ŵ��ļ���·��
    //            HTTPAssetBundlePath = Path.Combine(HTTPAddress);

    //            // ���ص�AB���ļ���·����AssetBundleLoadPath
    //            DownloadPath = Path.Combine(Application.persistentDataPath, "DownloadAssetBundle");
    //            AssetBundleLoadPath = Path.Combine(DownloadPath);

    //            if (!Directory.Exists(AssetBundleLoadPath))
    //            {
    //                Directory.CreateDirectory(AssetBundleLoadPath);
    //            }
    //            break;
    //    }

    //}

}

