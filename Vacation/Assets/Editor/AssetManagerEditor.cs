using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Unity.Plastic.Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

[Serializable]
public class PackageEditorInfo
{
    // �����а�������
    public string PackageName;

    // �����й����ڵ�ǰ���е���Դ�б�
    public List<UnityEngine.Object> AssetList = new List<UnityEngine.Object>();
}

public class AssetManagerEditor
{
    public class AssetBundleEdge 
    {
        // ���û����õ�Nodes
        public List<AssetBundleNode> Nodes = new List<AssetBundleNode>();
    }

    public class AssetBundleNode 
    {
        // ��ǰNode���������Դ���ƣ��������Դ��Ψһ�ԣ�����ʹ��GUID����
        public string AssetName;

        // Node��������SourceAsset >= 0
        public int SourceIndex = -1;

        // ��������Դ������SourceAsset��Index��SourceIndices��͸ÿһ��������ϵ���ֳ�����
        public List<int> SourceIndices = new List<int>();

        // ֻ��SourceAsset�ž��а���
        public string PackageName;

        // DerivedAsset��ֻ��PackageNames��������ù�ϵ
        public List<string> PackageNames = new List<string>();

        public AssetBundleEdge InEdge; // ���ø���Դ�Ľڵ㣬ֻ���ֳ�һ��������ϵ
        public AssetBundleEdge OutEdge;   // ����Դ���õĽڵ㣬ֻ���ֳ�һ��������ϵ
    }

    // ���δ������AssetBundle�����·����Ӧ����������������������������
    public static string AssetBundleOutputPath;

    // ��������������ļ������·��
    public static string BuildOutputPath;

    public static AssetManagerConfigScriptableObject AssetManagerConfig;
    public static AssetManagerEditorWindowConfig AssetManagerWindowConfig;

    // �ڹ������д��AB�����ô������������Unity����ǰ���ú�Ҫ����İ�
    [MenuItem(nameof(AssetManagerEditor) + "/" + nameof(BuildAssetBundle))]
    static void BuildAssetBundle()
    {
        CheckBuildOutputPath();
        
        // Directory������PCƽ̨��Ŀ¼���в�������
        if (!Directory.Exists(AssetBundleOutputPath))
        {
            Directory.CreateDirectory(AssetBundleOutputPath);
        }

        // ��ͬƽ̨֮���AssetBundle������ͨ�ã��÷����������������������˰�����AB�����������Ŀ¼�������ʽ�����ƽ̨
        BuildPipeline.BuildAssetBundles(AssetBundleOutputPath, BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows);
        AssetDatabase.Refresh(); // ˢ��
        Debug.Log($"AB�������ɣ�����·��Ϊ��{AssetBundleOutputPath}");
    }

    // ����MenuItem��������һ����̬����,�Դ���Window��
    [MenuItem("AssetManagerEditor/OpenAssetManagerWindow")]
    static void OpenAssetManagerEditorWindow()
    {
        // ���ʱִ�и÷���(���ð�),����һ�����ڣ�����һ����ΪAssetManagerName�Ĵ���
        AssetManagerEditorWindow window = EditorWindow.GetWindow<AssetManagerEditorWindow>("AssetManager");
    }

    // ͨ����̬����������ScriptableObject�ļ�(�ڹ������е������)
    [MenuItem("AssetManagerEditor/CreateConfigFile")] 
    public static void CreateConfigScriptableObject()
    {
        // ����ScriptableObject ���͵�ʵ������������JSON�н�ĳ�����ʵ�����Ĺ���
        AssetManagerConfigScriptableObject assetManagerConfig = ScriptableObject.CreateInstance<AssetManagerConfigScriptableObject>();
        AssetDatabase.CreateAsset(assetManagerConfig, "Assets/Editor/AssetManagerConfig.asset");
        AssetDatabase.SaveAssets();  // ������Դ
        AssetDatabase.Refresh(); // ˢ�±���Ŀ¼
    }

    // ������ĸ����ģʽ�������ڴ����д��AB����·��
    static void CheckBuildOutputPath()
    {
        switch (AssetManagerConfig.BuildingPattern)
        {
            case AssetBundlePattern.EditorSimulation:
                // �༭��ģʽ�£������д��
                break;
            case AssetBundlePattern.Local:
                // ����ģʽ�������StreamingAssets
                BuildOutputPath = Path.Combine(Application.streamingAssetsPath, "BuildOutput");
                break;
            case AssetBundlePattern.Remote:
                // Զ��ģʽ�����������Զ��·��(��C����)
                BuildOutputPath = Path.Combine(Application.persistentDataPath, "BuildOutput");
                break;
        }

        if (!Directory.Exists(BuildOutputPath))
        {
            Directory.CreateDirectory(BuildOutputPath);
        }

        // �ڴ����д����·����.../BuildOutput/LocalAssets
        AssetBundleOutputPath = Path.Combine(BuildOutputPath, "LocalAssets");

        if (!Directory.Exists(AssetBundleOutputPath))
        {
            Directory.CreateDirectory(AssetBundleOutputPath);
        }
    }
   
    static BuildAssetBundleOptions CheckCompressionPattern()
    {
        BuildAssetBundleOptions option = new BuildAssetBundleOptions();
        switch (AssetManagerConfig.CompressionPattern) 
        {
            case AssetBundleCompressionPattern.LZMA:
                option = BuildAssetBundleOptions.None;
                break;
            case AssetBundleCompressionPattern.LZ4:
                option = BuildAssetBundleOptions.ChunkBasedCompression;
                break;
            case AssetBundleCompressionPattern.None:
                option = BuildAssetBundleOptions.UncompressedAssetBundle;
                break;
        }
        return option;
    }

    static BuildAssetBundleOptions CheckIncrementalMode()
    {
        BuildAssetBundleOptions options = BuildAssetBundleOptions.None;

        switch (AssetManagerConfig.BuildMode) 
        {
            case IncrementalBuildMode.None:
                options = BuildAssetBundleOptions.None;
                break;
            case IncrementalBuildMode.UseIncrementalBuild:
                options = BuildAssetBundleOptions.DeterministicAssetBundle;
                break;
            case IncrementalBuildMode.ForceRebuild:
                options = BuildAssetBundleOptions.ForceRebuildAssetBundle;
                break;
        }
        return options;
    }

    // ��������ͼ����ʵallNodes �����ó�Ա���������棬�Ͳ���Ҫ�ٺ����д�����
    static void BuildDirectedGraph(AssetBundleNode lastNode, List<AssetBundleNode> allNodes)
    {
        if (lastNode == null)
        {
            Debug.Log("lastNodeΪ�գ���������ͼʧ��");
        }
        // ֻ��ȡֱ�ӵ�����
        string[] depends = AssetDatabase.GetDependencies(lastNode.AssetName, false);

        // ��������Դ��������0�������Ѿ��ߵ������ù�ϵ�����յ㣬Ҳ��������ͼ���յ㣬�������Ϸ���
        if (depends.Length <= 0)
        {
            return;
        }

        // OutEdgeΪ�մ���û������������������depends > 0 ����϶�����������Դ
        if (lastNode.OutEdge == null)
        {
            lastNode.OutEdge = new AssetBundleEdge();
        }

        // һ����Դ��ֱ����������Դ�����޵ģ���Ϊx
        foreach (var dependName in depends)
        {

            // ÿһ����Ч����Դ����Ϊһ���µ�Node
            AssetBundleNode currentNode = null;

            // ���Ѿ����ڵ�Node��һ���Ƚϴ��ֵ����Ϊn������ͼ���������У�Ƕ�ײ���Ϊ y
            // ���Ŀ��ܵı�������Ϊ y*x*n������ʵ���ϵı��������϶�����n*n
            foreach (AssetBundleNode existingNode in allNodes)
            {
                // �����Դ�Ѿ������ڵ㣬��ôֱ���������нڵ�
                if (existingNode.AssetName == dependName)
                {
                    currentNode = existingNode;
                    break;
                }
            }

            // ����������нڵ㣬������һ���½ڵ�
            if (currentNode == null)
            {
                currentNode = new AssetBundleNode();
                currentNode.AssetName = dependName;

                // ��Ϊ��ǰNode�ض���LastNode�������������Ա�Ȼ����InEdge��SourceIndices
                currentNode.InEdge = new AssetBundleEdge();
                currentNode.SourceIndices = new List<int>();
                allNodes.Add(currentNode);
            }

            currentNode.InEdge.Nodes.Add(lastNode);
            lastNode.OutEdge.Nodes.Add(currentNode);

            // �����Լ�������Դ�����ã�ͬ��Ҳͨ������ͼ���д���
            if (!string.IsNullOrEmpty(lastNode.PackageName))
            {
                if (!currentNode.PackageNames.Contains(lastNode.PackageName))
                {
                    currentNode.PackageNames.Add(lastNode.PackageName);
                }

            }
            else // ������DerivedAsset,ֱ�ӻ�ȡlastNOde��SourceIndices����
            {
                foreach (string packageNames in lastNode.PackageNames)
                {
                    if (!currentNode.PackageNames.Contains(packageNames))
                    {
                        currentNode.PackageNames.Add(packageNames);
                    }
                }
            }

            // ���lastNode��SourceAsset����ֱ��Ϊ��ǰ��Node���lastNode��Index
            // ��ΪList��һ���������ͣ�����SourceAsset��SourceIndices���º����ݺ�Drivedһ����Ҳ��Ϊһ���µ�List
            if (lastNode.SourceIndex >= 0)
            {
                if (!currentNode.SourceIndices.Contains(lastNode.SourceIndex))
                {
                    currentNode.SourceIndices.Add(lastNode.SourceIndex);
                }
                
            }
            else // DerivedAsset,ֱ�ӻ�ȡlastNOde��SourceIndices����
            {
                foreach(int index in lastNode.SourceIndices)
                {
                    if (!currentNode.SourceIndices.Contains(index))
                    {
                        currentNode.SourceIndices.Add(index);
                    }
                }
            }
            BuildDirectedGraph(currentNode, allNodes);
        } 
    }
    
    // ������ͼ�й���AB��
    public static void BuildAssetBundleFromDirectedGraph()
    {
        CheckBuildOutputPath();

        List<AssetBundleNode> allNodes = new List<AssetBundleNode>();
        int sourceIndex = 0;
        Dictionary<string, PackageBuildInfo> packageInfoDic = new Dictionary<string, PackageBuildInfo>();
        
        #region ����ͼ����

        for (int i = 0; i < AssetManagerConfig.packageInfoEditors.Count; i++)
        {
            PackageBuildInfo packageBuildInfo = new PackageBuildInfo();
            packageBuildInfo.PackageName = AssetManagerConfig.packageInfoEditors[i].PackageName;
            packageBuildInfo.IsSourcePackage = true;
            packageInfoDic.Add(packageBuildInfo.PackageName, packageBuildInfo);

            // ��ǰ��ѡ�е���Դ������SourceAsset,�����������SourceAsset��Node
            foreach(UnityEngine.Object asset in AssetManagerConfig.packageInfoEditors[i].AssetList)
            {
                AssetBundleNode currentNode = null;

                // ����Դ�ľ���·������Ϊ��Դ����
                string assetNamePath = AssetDatabase.GetAssetPath(asset);

                foreach(AssetBundleNode node in allNodes)
                {
                    if(node.AssetName == assetNamePath)
                    {
                        currentNode = node;
                        currentNode.PackageName = packageBuildInfo.PackageName;
                        break;
                    }
                }

                if(currentNode == null)
                {
                    currentNode = new AssetBundleNode();
                    currentNode.AssetName = assetNamePath;

                    // ΪʲôSourceAsset������SourceIndex����Ҫʹ��SourceIndices��
                    // ������Ϊ����ʹ��OutEdgeֱ��ʹ��SourceAsset��SourceIndices
                    currentNode.SourceIndex = sourceIndex;
                    currentNode.SourceIndices = new List<int>() { sourceIndex };

                    currentNode.PackageName = packageBuildInfo.PackageName;
                    currentNode.PackageNames.Add(currentNode.PackageName);

                    currentNode.InEdge = new AssetBundleEdge();
                    allNodes.Add(currentNode);
                }
                
                BuildDirectedGraph(currentNode, allNodes);
                
                sourceIndex++;
            }
        }
        #endregion

        #region ����ͼ�������
        // key����SourceIndices��key��ͬ��Node��Ӧ����ӵ�ͬһ��������
        Dictionary<List<int>, List<AssetBundleNode>> assetBundleNodesDic = new Dictionary<List<int>, List<AssetBundleNode>>();

        foreach(AssetBundleNode node in allNodes)
        {
            StringBuilder packageNameString = new StringBuilder();

            // ������Ϊ�ջ��ޣ��������һ��SourceAsset��������Ѿ��ڱ༭�������������
            if (string.IsNullOrEmpty(node.PackageName))
            {
                for(int i = 0; i < node.PackageNames.Count; i++)
                {
                    packageNameString.Append(node.PackageNames[i]);
                    if(i < node.PackageNames.Count - 1)
                    {
                        packageNameString.Append("_");
                    }
                }

                string packageName = packageNameString.ToString();
                node.PackageName = packageName;

                // ��ʱֻ����˶�Ӧ�İ��Լ���������û�о�����Ӱ��ж�Ӧ��Asset
                // ��ΪAsset�������Ҫ����AssetBundleName������ֻ��������AssetBundleBuild�ĵط����Asset
                if (!packageInfoDic.ContainsKey(packageName))
                {
                    PackageBuildInfo packageBuildInfo = new PackageBuildInfo();
                    packageBuildInfo.PackageName = packageName;
                    packageBuildInfo.IsSourcePackage = false;
                    packageInfoDic.Add(packageBuildInfo.PackageName, packageBuildInfo);
                }
            }

            bool isEquals = false;
            List<int> keyList = new List<int>();

            // �������е�key��ͨ�������ķ�ʽ����ȷ������ͬ��List֮�䣬������һ�µ�
            foreach(List<int> key in assetBundleNodesDic.Keys)
            {
                // �ж�key�ĳ����Ƿ�͵�ǰnode��SourceIndices�ĳ������
                isEquals = node.SourceIndices.Count == key.Count && node.SourceIndices.All(p => key.Any(k => k.Equals(p)));

                if (isEquals)
                {
                    keyList = key;
                    break;
                }
            }
            if (!isEquals)
            {
                keyList = node.SourceIndices;
                assetBundleNodesDic.Add(node.SourceIndices, new List<AssetBundleNode>());
            }

            // Node�ڹ���ʱ���ܱ�֤�϶������ظ�
            assetBundleNodesDic[keyList].Add(node);
        }
        #endregion

        AssetBundleBuild[] assetBundleBuilds = new AssetBundleBuild[assetBundleNodesDic.Count];
        int buildIndex = 0;
        foreach(var key in assetBundleNodesDic.Keys)
        {
            assetBundleBuilds[buildIndex].assetBundleName = buildIndex.ToString();
            List<string> assetNames = new List<string>();
            foreach(var node in assetBundleNodesDic[key])
            {
                assetNames.Add(node.AssetName);
                //Debug.Log($"Keyֵ�ĳ���={key.Count}������Node��{node.AssetName}");

                // ���һ��SourceAsset��������PackageNameֻ������Լ�
                foreach(string packageName in node.PackageNames)
                {
                    if (packageInfoDic.ContainsKey(packageName))
                    {
                        if(!packageInfoDic[packageName].PackageDependecies.Contains(node.PackageName)&&
                            !string.Equals(node.PackageName, packageInfoDic[packageName].PackageName))
                        {
                            packageInfoDic[packageName].PackageDependecies.Add(node.PackageName);
                        }
                    }
                }
            }

            // ����Ĳ�����ÿһ��AssetBundle�����в�������Asset·��
            // �����������Asset·��û�з����ı䣬�����ð�û�и�������
            assetBundleBuilds[buildIndex].assetBundleName = ComputeAssetSetSignature(assetNames);
            assetBundleBuilds[buildIndex].assetNames = assetNames.ToArray();

            foreach (AssetBundleNode node in assetBundleNodesDic[key])
            {

                // ��Ϊ�����˵�DerivedPackage�����Դ˴�����ȷ����ÿһ��Node������һ������
                AssetBuildInfo assetBuildInfo = new AssetBuildInfo();

                assetBuildInfo.AssetName = node.AssetName;
                assetBuildInfo.AssetBundleName = assetBundleBuilds[buildIndex].assetBundleName;

                packageInfoDic[node.PackageName].AssetInfos.Add(assetBuildInfo);

            }
            buildIndex++;
        }

        BuildPipeline.BuildAssetBundles(AssetBundleOutputPath, assetBundleBuilds, CheckIncrementalMode(), BuildTarget.StandaloneWindows);
        Debug.Log($"AB�������ɣ�����·��Ϊ��{AssetBundleOutputPath}");

        // ���汾��(����)д���ļ�
        string buildVersionFilePath = Path.Combine(BuildOutputPath, "BuildVersion.version");
        File.WriteAllText(buildVersionFilePath, AssetManagerConfig.CurrentBuildVersion.ToString());

        // �����汾·�����ļ�������Ϊ�汾��
        string versionPath = Path.Combine(BuildOutputPath, AssetManagerConfig.CurrentBuildVersion.ToString());
        if (!Directory.Exists(versionPath))
        {
            Directory.CreateDirectory(versionPath);
        }

        BuildAssetBundleHashTable(assetBundleBuilds,versionPath); // ����hash��

        CopyAssetBundleToVersionFolder(versionPath); // �������и���һ�ݵ��汾��(�ļ�����)��

        BuildPackageTable(packageInfoDic, versionPath); // ����Package��Ϣ

        CreateBuildInfo(versionPath); // ����BuildInfo��Ϣ

        AssetManagerConfig.CurrentBuildVersion++;

        AssetDatabase.Refresh();
    }

    // ��������AssetBundle������Asset����AssetGUIDת����byte[]
    static string ComputeAssetSetSignature(IEnumerable<string> assetNames)
    {
        var assetGuids = assetNames.Select(AssetDatabase.AssetPathToGUID);
        MD5 md5 = MD5.Create();

        // ��������asset������·������ͬ����ô���Եõ���ͬ��MD5��ϣֵ
        foreach(string assetGuid in assetGuids.OrderBy(x => x))
        {
            byte[] buffer = Encoding.ASCII.GetBytes(assetGuid);
            md5.TransformBlock(buffer, 0, buffer.Length, null, 0);
        }

        md5.TransformFinalBlock(new byte[0], 0, 0);
        return BytesToHexString(md5.Hash);
    }

    // byteת16�����ַ���
    static string BytesToHexString(byte[] bytes)
    {
        StringBuilder byteString = new StringBuilder();
        foreach (byte aByte in bytes)
        {
            byteString.Append(aByte.ToString("x2"));
        }
        return byteString.ToString();
    }

    // ����AB����hash������¼AB���Ĵ�С��hashֵ
    static string[] BuildAssetBundleHashTable(AssetBundleBuild[] assetBundleBuilds, string versionPath)
    {
        // ��ĳ�����AssetBundle����������һ��
        string[] assetBundleHashs = new string[assetBundleBuilds.Length];
        for(int i = 0; i < assetBundleBuilds.Length; i++)
        {
            string assetBundlePath = Path.Combine(AssetBundleOutputPath, assetBundleBuilds[i].assetBundleName);
           
            FileInfo fileInfo = new FileInfo(assetBundlePath);

            // ���м�¼�ģ���һ��AssetBundle�ļ��ĳ��ȣ��Լ������ݵ�MD5��ϣֵ
            assetBundleHashs[i] = $"{fileInfo.Length}_{assetBundleBuilds[i].assetBundleName}";

        }

        // д���ļ�
        string hashString = JsonConvert.SerializeObject(assetBundleHashs);
        string hashFilePath = Path.Combine(AssetBundleOutputPath, "AssetBundleHashs");
        string hashFileVersionPath = Path.Combine(versionPath, "AssetBundleHashs");
        File.WriteAllText(hashFilePath, hashString);
        File.WriteAllText(hashFileVersionPath, hashString);
        return assetBundleHashs; 
    }

    // ��ȡ��Ӧ·���µ�hash��
    static string[] ReadAssetBundleHashTable(string outputPath)
    {
        string hashTablePath = Path.Combine(outputPath, "AssetBundleHashs");
        string hashString = File.ReadAllText(hashTablePath);
        string[] assetHashs = JsonConvert.DeserializeObject<string[]>(hashString);
        return assetHashs;
    }
    static void CopyAssetBundleToVersionFolder(string versionPath)
    {
        // ��AssetBundle���·���¶�ȡhash��
        string[] assetNames = ReadAssetBundleHashTable(AssetBundleOutputPath);

        // ��������
        string mainBundleOriginPath = Path.Combine(AssetBundleOutputPath, "LocalAssets");
        string mainBundleVersionPath = Path.Combine(versionPath, "LocalAssets");

        //str_1ΪҪ���Ƶ�·���Լ��ļ�����
        //str_2ΪҪճ����·���Լ��ļ��������Բ�Ϊ��ͬ���ļ����ƣ����Ǻ�׺������ͬ��
        // ���� LocalAssets �ļ�
        File.Copy(mainBundleOriginPath, mainBundleVersionPath, true);

        // ��hash���е�ÿһ��������һ��
        foreach (var assetName in assetNames)
        {
            string assetHashName = assetName.Substring(assetName.IndexOf("_") + 1);

            string assetOriginPath = Path.Combine(AssetBundleOutputPath, assetHashName);
 
            string assetVersionPath = Path.Combine(versionPath, assetHashName);

            File.Copy(assetOriginPath, assetVersionPath, true); // ��һ��true����ʾ����
        }
    }

    // ��ScriptableObject�����ݱ���ΪJson��ʽ
    public static void SaveConfigToJSON()
    {
        if(AssetManagerConfig != null)
        {
            string configString = JsonUtility.ToJson(AssetManagerConfig);
            string configSavePath = Path.Combine(Application.dataPath, "Editor/AssetManagerConfig.json");
            File.WriteAllText(configSavePath, configString);
            AssetDatabase.Refresh();
        }
    }

    // �ȼ���Config�ļ�
    public static void LoadAssetManagerConfig(AssetManagerEditorWindow window)
    {
        if (AssetManagerConfig == null)
        {
            // ʹ��AssetDataBase������Դ��ֻ��Ҫ����AssetsĿ¼�µ�·������
            AssetManagerConfig = AssetDatabase.LoadAssetAtPath<AssetManagerConfigScriptableObject>(
                                                                    "Assets/Editor/AssetManagerConfig.asset");
            window.VersionString = AssetManagerConfig.AssetManagerVersion.ToString();
            for (int i = window.VersionString.Length - 1; i >= 1; i--)
            {
                window.VersionString = window.VersionString.Insert(i, ".");
            }
        }
    }

    // �ٴ�Json�ж�ȡConfig����ȡ�󣬴����е����ݺ�Json�е�һ��
    public static void ReadConfigFromJSON()
    {
        string configPath = Path.Combine(Application.dataPath, "Editor/AssetManagerConfig.json");
        string configString = File.ReadAllText(configPath);
        JsonUtility.FromJsonOverwrite(configString, AssetManagerConfig);
    }

    // ������ʾ������WindowConfig�ļ�
    public static void LoadAssetManagerWindowConfig(AssetManagerEditorWindow window)
    {
        if (window.WindowConfig == null)
        {
            // ʹ��AssetDataBase������Դ��ֻ��Ҫ����AssetsĿ¼�µ�·������
            window.WindowConfig = AssetDatabase.LoadAssetAtPath<AssetManagerEditorWindowConfig>(
                                                              "Assets/Editor/AssetManagerWindowConfig.asset");

            #region ����LOGO
            window.WindowConfig.LogoTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Images/1.jpg");
            window.WindowConfig.LogoTextureStyle = new GUIStyle();
            window.WindowConfig.LogoTextureStyle.fixedWidth = window.WindowConfig.LogoTexture.width / 2;
            window.WindowConfig.LogoTextureStyle.fixedHeight = window.WindowConfig.LogoTexture.height / 2;
            #endregion

            #region ����
            window.WindowConfig.TitleTextStyle = new GUIStyle();
            window.WindowConfig.TitleTextStyle.fontSize = 24;
            window.WindowConfig.TitleTextStyle.normal.textColor = Color.red;
            window.WindowConfig.TitleTextStyle.alignment = TextAnchor.MiddleCenter;
            #endregion

            #region �汾��
            window.WindowConfig.VersionTextStyle = new GUIStyle();
            window.WindowConfig.VersionTextStyle.fontSize = 16;
            window.WindowConfig.VersionTextStyle.normal.textColor = Color.green;
            window.WindowConfig.VersionTextStyle.alignment = TextAnchor.MiddleCenter;
            #endregion
        }
    }

    // �ڴ���������һ��Package
    public static void AddPackageInfoEditor()
    {
        AssetManagerConfig.packageInfoEditors.Add(new PackageEditorInfo());
    }

    // �ڴ������Ƴ�һ��Package
    public static void RemovePackageInfoEditors(PackageEditorInfo info)
    {
        if (AssetManagerConfig.packageInfoEditors.Contains(info))
        {
            AssetManagerConfig.packageInfoEditors.Remove(info);
        }
    }

    // �ڴ���������һ��Asset
    public static void AddAsset(PackageEditorInfo info)
    {
        info.AssetList.Add(null);
    }

    // �ڴ������Ƴ�һ��Asset
    public static void RemoveAsset(PackageEditorInfo info,UnityEngine.Object asset)
    {
        if (info.AssetList.Contains(asset))
        {
            info.AssetList.Remove(asset);
        }
    }

    public static string PackageTableName = "AllPackages"; // ��¼���еİ�������

    // ����Package�б�
    static void BuildPackageTable(Dictionary<string,PackageBuildInfo> packages, string versionPath)
    {
        string packasgesPath = Path.Combine(AssetBundleOutputPath, PackageTableName);
        string packagesVersionPath = Path.Combine(versionPath, PackageTableName);

        // Package�ֵ䣬keyΪ������������д���ļ�
        string packagesString = JsonConvert.SerializeObject(packages.Keys);
        File.WriteAllText(packasgesPath, packagesString);
        File.WriteAllText(packagesVersionPath, packagesString);

        foreach (PackageBuildInfo package in packages.Values)
        {
            packasgesPath = Path.Combine(AssetBundleOutputPath, package.PackageName);
            packagesVersionPath = Path.Combine(versionPath, package.PackageName);

            packagesString = JsonConvert.SerializeObject(package);
            File.WriteAllText(packasgesPath, packagesString);
            File.WriteAllText(packagesVersionPath, packagesString);
        }
    }

    // ����BuildInfo�ļ�
    public static void CreateBuildInfo(string versionPath)
    {
        BuildInfos currentBuildInfo = new BuildInfos();
        currentBuildInfo.BuildVersion = AssetManagerConfig.CurrentBuildVersion;

        // ��ȡAB�����·�����ļ�����Ϣ
        DirectoryInfo directoryInfo = new DirectoryInfo(AssetBundleOutputPath);

        // ��ȡ���ļ��������е��ļ���Ϣ
        FileInfo[] fileInfos = directoryInfo.GetFiles();

        // �������ļ����������ļ������ռ������ļ��ĳ���
        foreach(FileInfo fileInfo in fileInfos)
        {
            currentBuildInfo.FileNames.Add(fileInfo.Name, (ulong)fileInfo.Length);
            currentBuildInfo.FizeTotalSize += (ulong)fileInfo.Length;
        }

        string buildInfoSavePath = Path.Combine(versionPath, "BuildInfo");
        string buildInfoString = JsonConvert.SerializeObject(currentBuildInfo);

        File.WriteAllTextAsync(buildInfoSavePath, buildInfoString);
    }
}

