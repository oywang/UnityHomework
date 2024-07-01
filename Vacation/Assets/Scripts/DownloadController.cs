using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;


public class DownloadController : MonoBehaviour
{
    // ���ļ��ŵ�HPS�У���ȡ��·��
    public string HTTPAddress = "http://192.168.8.83:8080/"; // Զ�˷�������ַ
    private string RemoteVersionPath; // Զ�˰汾·��
    private string DownloadVersionPath; // ���ص����صİ汾·��
    BuildInfos RemoteBuildInfos;
    DownloadedInfos CurrentDownloadedInfos = new DownloadedInfos(); // ��¼��ǰ�Ѿ������˵��ļ�

    // �Ƚϰ汾�еĲ�ͬ
    public class AssetBundleVersionDifference
    {
        public List<string> AddtionAssetBundles;  // ���ΰ汾�Ա������ӵ�AssetBundle
        public List<string> ReducesAssetBundels;  // ���ΰ汾�Ա��м��ٵ�AssetBundle
    }

    // ��¼�Ѿ������˵���Ϣ
    public class DownloadedInfos
    {
        // ���ڴ��浱ǰ������Ϣ���Ѿ����ص��ļ���
        public List<string> DownloadedFileNames = new List<string>();
    }
    void Start()
    {
        // ģʽ����ΪRemote
        AssetManagerRuntime.AssetManagerInit(AssetBundlePattern.Remote);
        StartCoroutine(GetRemoteVersion());   // ������Դ
    }

    private void Update()
    {
        // ���ԣ�����Դ���ض�AB����
        if (Input.GetKeyDown(KeyCode.Space))
        {
            LoadAsset();
        }
    }


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
            yield return null; // ����null����ȴ�һ֡
        }
        if (!string.IsNullOrEmpty(request.error))
        {
            Debug.LogError(request.error);
            yield break;
        }

        // ��¼�汾��
        int version = int.Parse(request.downloadHandler.text); // ����汾�ţ����ַ���ת������
        AssetManagerRuntime.Instance.RemoteAssetVersion = version; // ʹ�ñ�������Զ�˰汾

        #endregion

        // Զ�˰汾·�������ذ汾·��
        RemoteVersionPath = Path.Combine(HTTPAddress, "BuildOutput", AssetManagerRuntime.Instance.RemoteAssetVersion.ToString());
        DownloadVersionPath = Path.Combine(AssetManagerRuntime.Instance.DownloadPath, AssetManagerRuntime.Instance.RemoteAssetVersion.ToString());
       

        // �ж� �汾�ļ��� �Ƿ����
        if (!Directory.Exists(DownloadVersionPath))
        {
            Directory.CreateDirectory(DownloadVersionPath);
        }
        Debug.Log($"����·����{DownloadVersionPath}");

        #region ��ȡԶ��BuildInfo

        // BuildInfo�а����汾�š������ļ��������ļ����ܴ�С���ڴ��ABʱ����Ϣд���ļ�
        string remoteBuildInfosPath = Path.Combine(HTTPAddress, "BuildOutput", version.ToString(), "BuildInfo");

        // ����Web����
        request = UnityWebRequest.Get(remoteBuildInfosPath);
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

        // ����������Ϣ
        string buildInfoString = request.downloadHandler.text;
        RemoteBuildInfos = JsonConvert.DeserializeObject<BuildInfos>(buildInfoString);

        if(RemoteBuildInfos == null || RemoteBuildInfos.FizeTotalSize <= 0)
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
        string downloadInfoPath = Path.Combine(AssetManagerRuntime.Instance.DownloadPath, "TempDownloadInfo");
        if (File.Exists(downloadInfoPath))
        {
            // ��ȡ�ļ��Ѿ������˵���Ϣ
            string downloadInfoString = File.ReadAllText(downloadInfoPath);

            // ����ȡ������Ϣ�����л�ΪDownloadedInfos����
            CurrentDownloadedInfos = JsonConvert.DeserializeObject<DownloadedInfos>(downloadInfoString);
        }
        else // �ļ������ڣ����½�һ��
        {
            CurrentDownloadedInfos = new DownloadedInfos();
        }

        // ���Ȼ���Ҫ����AllPackages�Լ�Packages��������Ҫ�ж�AllPackages�Ƿ��Ѿ�����
        if (CurrentDownloadedInfos.DownloadedFileNames.Contains("AllPackages"))
        {
            OnCompleted("AllPackages", "���ڱ��ش���");
        }
        else
        {
            string filePath = Path.Combine(RemoteVersionPath, "AllPackages");
            string savePath = Path.Combine(DownloadVersionPath, "AllPackages");

            // �ӽ�Զ��filePath��AllPackages���ص�����savePath
            Downloader downloader = new Downloader(filePath, savePath, OnCompleted, OnProgress, OnError);
            downloader.StartDownload();
        }
    }
   
    // ���ʱ�ص�
    void OnCompleted(string fileName, string message)
    {
        // ��������ļ��б���������ļ�����ֱ��ִ��Completed�¼��������������
        if (!CurrentDownloadedInfos.DownloadedFileNames.Contains(fileName))
        {
            // ��������ɵ��ļ��İ�����ӵ��Ѿ����ص��б���
            CurrentDownloadedInfos.DownloadedFileNames.Add(fileName);

            // ����ǰ�Ѿ������˵��ļ����浽��������Ŀ¼TempDownloadInfo��
            string downloadSavePath = Path.Combine(AssetManagerRuntime.Instance.DownloadPath, "TempDownloadInfo");

            // ����ǰ�Ѿ����ص���Ϣ�����л����ַ���
            string downloadedInfosString = JsonConvert.SerializeObject(CurrentDownloadedInfos);

            // д�뵽TempDownloadInfo����
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

        // �����������ļ������ͷ������ļ�������ȣ�������������
        if (CurrentDownloadedInfos.DownloadedFileNames.Count == RemoteBuildInfos.FileNames.Count)
        {
            CopeDownloadAssetsToLocalPath();
            AssetManagerRuntime.Instance.UpdateLocalAssetVersion();
            LoadAsset();
        }
        Debug.Log($"{fileName}:{message}");
    }

    // ����AB��
    void LoadAsset()
    {
        AssetPackage package = AssetManagerRuntime.Instance.LoadPackage("A");
        GameObject obj = package.LoadAsset<GameObject>("Assets/SampleAssets/Cube.prefab");

        Instantiate(obj);
    }


    // �������������б�
    void CreatePackagesDownloadList()
    {
        // ��ȡAllPackages�ļ�������ÿһ����������
        string allPackagesPath = Path.Combine(DownloadVersionPath, "AllPackages");

        // ��ȡ�ļ�
        string allPackagesString = File.ReadAllText(allPackagesPath);

        // ����ȡ�����ַ��������л���List<string>����
        List<string> allPackages = JsonConvert.DeserializeObject<List<string>>(allPackagesString);

        Downloader downloader = null;

        // �������е�ÿһ����
        foreach (string packageName in allPackages)
        {
            // ����ļ�û�����أ��򽫸ð���������
            if (!CurrentDownloadedInfos.DownloadedFileNames.Contains(packageName))
            {
                string remotePackagePath = Path.Combine(RemoteVersionPath, packageName);
                string remotePackageSavePath = Path.Combine(DownloadVersionPath, packageName);

                // ��Զ�˽������ص�����
                downloader = new Downloader(remotePackagePath, remotePackageSavePath, OnCompleted, OnProgress, OnError);
                downloader.StartDownload();
            }
            else
            {
                OnCompleted(packageName, "�����Ѵ���");
            }
        }

        // �����ǰ�Ѿ����صİ��в�����AssertBundleHahes�����������hash��
        if (!CurrentDownloadedInfos.DownloadedFileNames.Contains("AssetBundleHashes"))
        {
            string remoteHashPath = Path.Combine(RemoteVersionPath, "AssetBundleHashes");
            string remoteHashSavePath = Path.Combine(DownloadVersionPath, "AssetBundleHashes");

            // ��Զ�˽�hash�����ص�����
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
        string assetBundleHashsString = File.ReadAllText(assetBundleHashsPath);

        // ��ȡԶ��hash��
        string[] remoteAssetBundleHashs = JsonConvert.DeserializeObject<string[]>(assetBundleHashsString);

        string localAssetBundleHashPath = Path.Combine(AssetManagerRuntime.Instance.AssetBundleLoadPath, "AssetBundleHashs");
        string assetBundleHashString = null;

        string[] localAssetBundleHash = null;

        if (File.Exists(localAssetBundleHashPath))
        {
            assetBundleHashsString = File.ReadAllText(localAssetBundleHashPath);
            localAssetBundleHash = JsonConvert.DeserializeObject<string[]>(assetBundleHashString);
        }

        // �ԱȽ����󣬰���AssetBundle��������AssetBundle
        List<string> downloadAssetNames = null;
        if (localAssetBundleHash == null)
        {
            Debug.Log("���ر��ȡʧ�ܣ�ֱ������Զ�˱�");
            downloadAssetNames = remoteAssetBundleHashs.ToList();
        }
        else
        {
            AssetBundleVersionDifference difference = ContrastAssetBundleVersion(localAssetBundleHash, remoteAssetBundleHashs);
            downloadAssetNames = difference.AddtionAssetBundles;
        }

        // �����������
        downloadAssetNames.Add("LocalAssets");
        Downloader downloader = null;
        foreach (string assetBundleName in downloadAssetNames)
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

            if (!CurrentDownloadedInfos.DownloadedFileNames.Contains(fileName))
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



    // �Ա������汾�Ĳ�ͬ
    static AssetBundleVersionDifference ContrastAssetBundleVersion(string[] oldVersion, string[] newVersion)
    {
        AssetBundleVersionDifference difference = new AssetBundleVersionDifference();
        difference.AddtionAssetBundles = new List<string>();
        difference.ReducesAssetBundels = new List<string>();

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

        Debug.Log(localVersionPath);
        directoryInfo.MoveTo(localVersionPath);

        string downloadInfoPath = Path.Combine(AssetManagerRuntime.Instance.DownloadPath, "TempDownloadInfo");
        File.Delete(downloadInfoPath);
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
}
