using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;



// 因为Assets下的其他脚本会被编译到AssetmblyCharp.dll中，跟随着包体打包出去(如APK），
// 所以不允许使用来自UnityEditor命名空间下的方法
public class HelloWorld : MonoBehaviour
{
    // Inspector面板中的打包模式
    public AssetBundlePattern LoadPattern;


    // 将文件放到HPS中，获取的路径
    public string HTTPAddress = "http://192.168.8.83:8080/";

    public string DownloadPath;

    public string RemoteVersionPath;
    public string DownloadVersionPath;

    public class AssetBundleVersionDifference
    {
        public List<string> AddtionAssetBundles;  // 本次版本对比中增加的AssetBundle
        public List<string> ReducesAssetBundels;  // 本次版本对比中减少的AssetBundle
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
    // 获取远端对比列表
    IEnumerator GetRemoteVersion()
    {
        #region 获取远端版本号
        string remoteVersionFilePath = Path.Combine(HTTPAddress, "BuildOutput", "BuildVersion.version");

        // 发送Web请求
        UnityWebRequest request = UnityWebRequest.Get(remoteVersionFilePath);
        request.SendWebRequest();

        while (!request.isDone)
        {
            // 返回null代表等待一帧
            yield return null;
        }

        if (!string.IsNullOrEmpty(request.error))
        {
            Debug.LogError(request.error);
            yield break;
        }

        // 保存版本号
        int version = int.Parse(request.downloadHandler.text);

        // 使用变量保存远端版本
        if (AssetManagerRuntime.Instance.LocalAssetVersion == version)
        {
            LoadAsset();
            yield break;
        }

        AssetManagerRuntime.Instance.RemoteAssetVersion = version;

        Debug.Log($"远端资源版本为：{version}");
        #endregion

        // 远端版本路径和下载版本路径
        RemoteVersionPath = Path.Combine(HTTPAddress, "BuildOutput", AssetManagerRuntime.Instance.RemoteAssetVersion.ToString());
        DownloadVersionPath = Path.Combine(AssetManagerRuntime.Instance.DownloadPath, AssetManagerRuntime.Instance.RemoteAssetVersion.ToString());

        if (!Directory.Exists(DownloadVersionPath))
        {
            Directory.CreateDirectory(DownloadVersionPath);
        }
        Debug.Log($"下载路径：{DownloadVersionPath}");

        #region 获取远端BuildInfo
        string remoteBuildInfoPath = Path.Combine(HTTPAddress, "BuildOutput", version.ToString(), "BuildInfo");

        // 发送Web请求
        request = UnityWebRequest.Get(remoteBuildInfoPath);
        request.SendWebRequest();
        while (!request.isDone)
        {
            // 返回null代表等待一帧
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
        // 创建下载列表
        CreateDownloadList();
    }
    // 创建下载列表
    void CreateDownloadList()
    {
        // 首先读取本地的下载列表，TempDownloadInfo里的内容{"DownloadedFileNames":["AllPackages"]}
        string downloadInfoPath = Path.Combine(AssetManagerRuntime.Instance.DownloadPath, DownloadInfoFileName);
        if (File.Exists(downloadInfoPath))
        {
            string downloadInfoString = File.ReadAllText(downloadInfoPath);
            CurrentDownloadInfo = JsonConvert.DeserializeObject<DownloadInfo>(downloadInfoString);
        }
        else // 文件不存在，则新建一个
        {
            CurrentDownloadInfo = new DownloadInfo();
        }

        // 首先还是要下载AllPackages以及Packages，所以先要判断AllPackages是否已经下载
        if (CurrentDownloadInfo.DownloadedFileNames.Contains("AllPackages"))
        {
            OnCompleted("AllPackages", "已在本地存在");
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

        // 对比每一个老版本的ab包(hash表)，如果新版本不存在该包，则视为需要减少的包
        foreach (var assetBundle in oldVersion)
        {
            if (!newVersion.Contains(assetBundle))
            {
                difference.ReducesAssetBundels.Add(assetBundle);
            }
        }

        // 对比每一个新版本的ab包(hash表)，如果老版本不存在该包，则视为需要新增的包
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
    // 完成时回调
    void OnCompleted(string fileName, string message)
    {
        // 如果本地文件列表中有这个文件，则直接执行Completed事件，否则进行下载
        if (!CurrentDownloadInfo.DownloadedFileNames.Contains(fileName))
        {
            // 将下载完成的文件添加到列表当中
            CurrentDownloadInfo.DownloadedFileNames.Add(fileName);

            // 将DownloadInfo保存到本地下载目录中
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
        // 如果下载完成文件数量和服务器文件数量相等，则代表下载完成
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
    // 下载时回调
    void OnProgress(float progress, long currentLength, long totalLength)
    {
        Debug.Log($"下载进度：{progress * 100}%，当前下载长度：{currentLength * 1.0f / 1024 / 1024}M,文件总长度：{totalLength * 1.0f / 1024 / 1024}M");

    }

    // 出错时回调
    void OnError(ErrorCode errorCode, string message)
    {
        Debug.LogError(message);
    }


    void CreatePackagesDownloadList()
    {
        string allPackagesPath = Path.Combine(DownloadVersionPath, "AllPackages");
        string allPackagesString = File.ReadAllText(allPackagesPath);
        Debug.Log($"下载的AllPackages内容：{allPackagesString}");
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
                OnCompleted(packageName, "本地已存在");
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
            OnCompleted("AssetBundleHashs", "本地已存在");
        }
    }

    void CreateAssetBundleDownloadList()
    {
        // 本地表读取路径
        string assetBundleHashsPath = Path.Combine(DownloadVersionPath, "AssetBundleHashs");
        string[] localAssetBundleHashs = null;

        if (File.Exists(assetBundleHashsPath))
        {
            string assetBundleHashsString = File.ReadAllText(assetBundleHashsPath);
            localAssetBundleHashs = JsonConvert.DeserializeObject<string[]>(assetBundleHashsString);
        }
        // 读取远端hash表
        string remoteHashPath = Path.Combine(DownloadVersionPath, "AssetBundleHashs");
        string remoteHashString = File.ReadAllText(remoteHashPath);
        string[] remoteAssetBundleHashs = JsonConvert.DeserializeObject<string[]>(remoteHashPath);

        // 本次需要更新下载的AB包
        List<string> downloadAssetNames = null;
        if(localAssetBundleHashs == null)
        {
            Debug.Log("本地读取失败，直接下载远端表");
            downloadAssetNames = remoteAssetBundleHashs.ToList();
        }
        else
        {
            AssetBundleVersionDifference difference = ContrastAssetBundleVersion(localAssetBundleHashs, remoteAssetBundleHashs);
            downloadAssetNames = difference.AddtionAssetBundles;
        }

        // 添加主包包名
        downloadAssetNames.Add("LocalAssets");
        Downloader downloader = null;
        foreach(string assetBundleName in downloadAssetNames)
        {
            // 因为hash列表中的文件名是由文件大小和文件名构成的，用下划线进行划分，
            // 所以要从下划线后一位开始获取AssetBundle的具体名称
            string fileName = assetBundleName;
            if (assetBundleName.Contains("_"))
            {
                // 下划线最后一位才是AssetBundleName
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
                OnCompleted(fileName, "本地已存在");
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
            // 返回null代表等待一帧
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
        Debug.Log($"AssetBundleHashs列表下载完成：{hashString}");

        CreateDownloadList();
        yield return null;
    }

    //// 从远端下载AB包
    //IEnumerator DownloadFile(string fileName, Action callBack, bool isSaveFile = true)
    //{
    //    // 远端AB包的文件夹路径
    //    string assetBundleDownloadPath = Path.Combine(HTTPAddress, fileName);

    //    // HPS中的名为SampleAssetBundle包的路径http://192.168.8.83:8080/SampleAssetBundle/SampleAssetBundle
    //    Debug.Log($"AB包从该路径下载：{assetBundleDownloadPath}");
    //    UnityWebRequest webRequest = UnityWebRequest.Get(assetBundleDownloadPath);

    //    yield return webRequest.SendWebRequest();

    //    while (!webRequest.isDone)
    //    {
    //        Debug.Log(webRequest.downloadedBytes); // 下载总字节数
    //        Debug.Log(webRequest.downloadProgress); // 下载进度
    //        yield return new WaitForEndOfFrame();
    //    }

    //    // AB包的路径
    //    string fileSavePath = Path.Combine(AssetBundleLoadPath, fileName);

    //    //C:/Users/wyh/AppData/LocalLow/DefaultCompany/Test\DownloadAssetBundle
    //    //                              \SampleAssetBundle\SampleAssetBundle
    //    Debug.Log($"远端AB包保存的地址为：{fileSavePath}");
    //    Debug.Log(webRequest.downloadHandler.data.Length);
    //    if (isSaveFile)
    //    {
    //        yield return SaveFile(fileSavePath, webRequest.downloadHandler.data, callBack);
    //    }
    //    else
    //    {
    //        // 三目运算符判断对象是否为空
    //        callBack?.Invoke();
    //    }
    //}

    //// 下载AB包
    //IEnumerator DownloadAssetBundle(List<string> fileNames, Action callBack = null)
    //{
    //    foreach (string fileName in fileNames)
    //    {
    //        string assetBundleName = fileName;
    //        if (fileName.Contains("_"))
    //        {
    //            // 下划线后一位才是AssetBundleName
    //            int startIndex = fileName.IndexOf("_") + 1;
    //            assetBundleName = fileName.Substring(startIndex);
    //        }

    //        string assetBundleDownloadPath = Path.Combine(HTTPAddress, "BuildOutput", AssetManagerRuntime.Instance.RemoteAssetVersion.ToString(), assetBundleName);

    //        UnityWebRequest request = UnityWebRequest.Get(assetBundleDownloadPath);

    //        request.SendWebRequest();

    //        while (!request.isDone)
    //        {
    //            // 返回null代表等待一帧
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

    //        Debug.Log($"AssetBundle下载完成{assetBundleName}");
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
    //        // 返回null代表等待一帧
    //        yield return null;
    //    }

    //    if (!string.IsNullOrEmpty(request.error))
    //    {
    //        Debug.LogError(request.error);
    //        yield break;
    //    }

    //    string allPackagesString = request.downloadHandler.text;

    //    string packagesSavePath = Path.Combine(AssetManagerRuntime.Instance.DownloadPath, AssetManagerRuntime.Instance.RemoteAssetVersion.ToString(), "AllPackages");

    //    // 将远端服务器的Package列表保存到本地下载路径
    //    File.WriteAllText(packagesSavePath, allPackagesString);
    //    Debug.Log($"Package的路径为：{packagesSavePath}");

    //    // AllPackage表格转成ListString,并下载对应包到本地
    //    List<string> packageNames = JsonConvert.DeserializeObject<List<string>>(allPackagesString);

    //    foreach (string packageName in packageNames)
    //    {
    //        remotePackagePath = Path.Combine(HTTPAddress, "BuildOutput", AssetManagerRuntime.Instance.RemoteAssetVersion.ToString(), packageName);

    //        request = UnityWebRequest.Get(remotePackagePath);

    //        request.SendWebRequest();

    //        while (!request.isDone)
    //        {
    //            // 返回null代表等待一帧
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
    //        Debug.Log($"package下载完毕：{packageName}");
    //    }

    //    StartCoroutine(GetRemoteAssetBundleHash());
    //    yield return null;
    //}

    // 将远程的文件写入到本地
    //IEnumerator SaveFile(string savePath, byte[] bytes, Action callBack)
    //{
    //    FileStream fileStream = File.Open(savePath, FileMode.OpenOrCreate);

    //    yield return fileStream.WriteAsync(bytes, 0, bytes.Length);

    //    // 释放文件流，否则文件会一直处于读取的状态而不能被其他进程读取
    //    fileStream.Flush();
    //    fileStream.Close();
    //    fileStream.Dispose();

    //    callBack?.Invoke();
    //    Debug.Log($"{savePath}文件保存完成");

    //}


    //void CheckAssetBundleLoadPath() // 检查包是哪种路径
    //{
    //    switch (LoadPattern)
    //    {
    //        case AssetBundlePattern.EditorSimulation:
    //            break;
    //        case AssetBundlePattern.Local:
    //            AssetBundleLoadPath = Path.Combine(Application.streamingAssetsPath);
    //            break;
    //        case AssetBundlePattern.Remote:
    //            // AB存放的文件夹路径
    //            HTTPAssetBundlePath = Path.Combine(HTTPAddress);

    //            // 下载的AB包文件夹路径：AssetBundleLoadPath
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

