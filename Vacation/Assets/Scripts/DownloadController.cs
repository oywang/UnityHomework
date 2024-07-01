using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;


public class DownloadController : MonoBehaviour
{
    // 将文件放到HPS中，获取的路径
    public string HTTPAddress = "http://192.168.8.83:8080/"; // 远端服务器地址
    private string RemoteVersionPath; // 远端版本路径
    private string DownloadVersionPath; // 下载到本地的版本路径
    BuildInfos RemoteBuildInfos;
    DownloadedInfos CurrentDownloadedInfos = new DownloadedInfos(); // 记录当前已经下载了的文件

    // 比较版本中的不同
    public class AssetBundleVersionDifference
    {
        public List<string> AddtionAssetBundles;  // 本次版本对比中增加的AssetBundle
        public List<string> ReducesAssetBundels;  // 本次版本对比中减少的AssetBundle
    }

    // 记录已经下载了的信息
    public class DownloadedInfos
    {
        // 用于储存当前下载信息，已经下载的文件名
        public List<string> DownloadedFileNames = new List<string>();
    }
    void Start()
    {
        // 模式设置为Remote
        AssetManagerRuntime.AssetManagerInit(AssetBundlePattern.Remote);
        StartCoroutine(GetRemoteVersion());   // 下载资源
    }

    private void Update()
    {
        // 测试，将资源加载都AB包中
        if (Input.GetKeyDown(KeyCode.Space))
        {
            LoadAsset();
        }
    }


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
            yield return null; // 返回null代表等待一帧
        }
        if (!string.IsNullOrEmpty(request.error))
        {
            Debug.LogError(request.error);
            yield break;
        }

        // 记录版本号
        int version = int.Parse(request.downloadHandler.text); // 保存版本号，将字符串转成整数
        AssetManagerRuntime.Instance.RemoteAssetVersion = version; // 使用变量保存远端版本

        #endregion

        // 远端版本路径和下载版本路径
        RemoteVersionPath = Path.Combine(HTTPAddress, "BuildOutput", AssetManagerRuntime.Instance.RemoteAssetVersion.ToString());
        DownloadVersionPath = Path.Combine(AssetManagerRuntime.Instance.DownloadPath, AssetManagerRuntime.Instance.RemoteAssetVersion.ToString());
       

        // 判断 版本文件夹 是否存在
        if (!Directory.Exists(DownloadVersionPath))
        {
            Directory.CreateDirectory(DownloadVersionPath);
        }
        Debug.Log($"下载路径：{DownloadVersionPath}");

        #region 获取远端BuildInfo

        // BuildInfo中包含版本号、所有文件和所有文件的总大小，在打包AB时将信息写入文件
        string remoteBuildInfosPath = Path.Combine(HTTPAddress, "BuildOutput", version.ToString(), "BuildInfo");

        // 发送Web请求
        request = UnityWebRequest.Get(remoteBuildInfosPath);
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

        // 储存打包的信息
        string buildInfoString = request.downloadHandler.text;
        RemoteBuildInfos = JsonConvert.DeserializeObject<BuildInfos>(buildInfoString);

        if(RemoteBuildInfos == null || RemoteBuildInfos.FizeTotalSize <= 0)
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
        string downloadInfoPath = Path.Combine(AssetManagerRuntime.Instance.DownloadPath, "TempDownloadInfo");
        if (File.Exists(downloadInfoPath))
        {
            // 读取文件已经下载了的信息
            string downloadInfoString = File.ReadAllText(downloadInfoPath);

            // 将读取到的信息反序列化为DownloadedInfos类型
            CurrentDownloadedInfos = JsonConvert.DeserializeObject<DownloadedInfos>(downloadInfoString);
        }
        else // 文件不存在，则新建一个
        {
            CurrentDownloadedInfos = new DownloadedInfos();
        }

        // 首先还是要下载AllPackages以及Packages，所以先要判断AllPackages是否已经下载
        if (CurrentDownloadedInfos.DownloadedFileNames.Contains("AllPackages"))
        {
            OnCompleted("AllPackages", "已在本地存在");
        }
        else
        {
            string filePath = Path.Combine(RemoteVersionPath, "AllPackages");
            string savePath = Path.Combine(DownloadVersionPath, "AllPackages");

            // 从将远端filePath的AllPackages下载到本地savePath
            Downloader downloader = new Downloader(filePath, savePath, OnCompleted, OnProgress, OnError);
            downloader.StartDownload();
        }
    }
   
    // 完成时回调
    void OnCompleted(string fileName, string message)
    {
        // 如果本地文件列表中有这个文件，则直接执行Completed事件，否则进行下载
        if (!CurrentDownloadedInfos.DownloadedFileNames.Contains(fileName))
        {
            // 将下载完成的文件的包名添加到已经下载的列表当中
            CurrentDownloadedInfos.DownloadedFileNames.Add(fileName);

            // 将当前已经下载了的文件保存到本地下载目录TempDownloadInfo中
            string downloadSavePath = Path.Combine(AssetManagerRuntime.Instance.DownloadPath, "TempDownloadInfo");

            // 将当前已经下载的信息，序列化成字符串
            string downloadedInfosString = JsonConvert.SerializeObject(CurrentDownloadedInfos);

            // 写入到TempDownloadInfo本地
            File.WriteAllText(downloadSavePath, downloadedInfosString);
        }

        switch (fileName)
        {
            case "AllPackages":
                CreatePackagesDownloadList(); 
                break;
            case "AssetBundleHashs":
                CreateAssetBundleDownloadList(); 
                break;
        }

        // 如果下载完成文件数量和服务器文件数量相等，则代表下载完成
        if (CurrentDownloadedInfos.DownloadedFileNames.Count == RemoteBuildInfos.FileNames.Count)
        {
            CopeDownloadAssetsToLocalPath();
            AssetManagerRuntime.Instance.UpdateLocalAssetVersion();
            LoadAsset();
        }
        Debug.Log($"{fileName}:{message}");
    }

    // 加载AB包
    void LoadAsset()
    {
        AssetPackage package = AssetManagerRuntime.Instance.LoadPackage("A");
        GameObject obj = package.LoadAsset<GameObject>("Assets/SampleAssets/Cube.prefab");

        Instantiate(obj);
    }


    // 创建包的下载列表
    void CreatePackagesDownloadList()
    {
        // 读取AllPackages文件，里面每一个包的名称
        string allPackagesPath = Path.Combine(DownloadVersionPath, "AllPackages");

        // 读取文件
        string allPackagesString = File.ReadAllText(allPackagesPath);

        // 将读取到的字符串反序列化成List<string>类型
        List<string> allPackages = JsonConvert.DeserializeObject<List<string>>(allPackagesString);

        Downloader downloader = null;

        // 遍历包中的每一个包
        foreach (string packageName in allPackages)
        {
            // 如果文件没有下载，则将该包进行下载
            if (!CurrentDownloadedInfos.DownloadedFileNames.Contains(packageName))
            {
                string remotePackagePath = Path.Combine(RemoteVersionPath, packageName);
                string remotePackageSavePath = Path.Combine(DownloadVersionPath, packageName);

                // 从远端将包下载到本地
                downloader = new Downloader(remotePackagePath, remotePackageSavePath, OnCompleted, OnProgress, OnError);
                downloader.StartDownload();
            }
            else
            {
                OnCompleted(packageName, "本地已存在");
            }
        }

        // 如果当前已经下载的包中不包含AssertBundleHahes，则进行下载hash表
        if (!CurrentDownloadedInfos.DownloadedFileNames.Contains("AssetBundleHashes"))
        {
            string remoteHashPath = Path.Combine(RemoteVersionPath, "AssetBundleHashes");
            string remoteHashSavePath = Path.Combine(DownloadVersionPath, "AssetBundleHashes");

            // 从远端将hash表下载到本地
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
        string assetBundleHashsString = File.ReadAllText(assetBundleHashsPath);

        // 读取远端hash表
        string[] remoteAssetBundleHashs = JsonConvert.DeserializeObject<string[]>(assetBundleHashsString);

        string localAssetBundleHashPath = Path.Combine(AssetManagerRuntime.Instance.AssetBundleLoadPath, "AssetBundleHashs");
        string assetBundleHashString = null;

        string[] localAssetBundleHash = null;

        if (File.Exists(localAssetBundleHashPath))
        {
            assetBundleHashsString = File.ReadAllText(localAssetBundleHashPath);
            localAssetBundleHash = JsonConvert.DeserializeObject<string[]>(assetBundleHashString);
        }

        // 对比结束后，按照AssetBundle名称下载AssetBundle
        List<string> downloadAssetNames = null;
        if (localAssetBundleHash == null)
        {
            Debug.Log("本地表读取失败，直接下载远端表");
            downloadAssetNames = remoteAssetBundleHashs.ToList();
        }
        else
        {
            AssetBundleVersionDifference difference = ContrastAssetBundleVersion(localAssetBundleHash, remoteAssetBundleHashs);
            downloadAssetNames = difference.AddtionAssetBundles;
        }

        // 添加主包包名
        downloadAssetNames.Add("LocalAssets");
        Downloader downloader = null;
        foreach (string assetBundleName in downloadAssetNames)
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

            if (!CurrentDownloadedInfos.DownloadedFileNames.Contains(fileName))
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



    // 对比两个版本的不同
    static AssetBundleVersionDifference ContrastAssetBundleVersion(string[] oldVersion, string[] newVersion)
    {
        AssetBundleVersionDifference difference = new AssetBundleVersionDifference();
        difference.AddtionAssetBundles = new List<string>();
        difference.ReducesAssetBundels = new List<string>();

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

        Debug.Log(localVersionPath);
        directoryInfo.MoveTo(localVersionPath);

        string downloadInfoPath = Path.Combine(AssetManagerRuntime.Instance.DownloadPath, "TempDownloadInfo");
        File.Delete(downloadInfoPath);
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
}
