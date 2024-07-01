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
    // 窗口中包的名称
    public string PackageName;

    // 窗口中归属于当前包中的资源列表
    public List<UnityEngine.Object> AssetList = new List<UnityEngine.Object>();
}

public class AssetManagerEditor
{
    public class AssetBundleEdge 
    {
        // 引用或被引用的Nodes
        public List<AssetBundleNode> Nodes = new List<AssetBundleNode>();
    }

    public class AssetBundleNode 
    {
        // 当前Node所代表的资源名称，代表该资源的唯一性，可以使用GUID代替
        public string AssetName;

        // Node的索引，SourceAsset >= 0
        public int SourceIndex = -1;

        // 依赖该资源的所有SourceAsset的Index，SourceIndices穿透每一层依赖关系体现出依赖
        public List<int> SourceIndices = new List<int>();

        // 只有SourceAsset才具有包名
        public string PackageName;

        // DerivedAsset的只有PackageNames代表别引用关系
        public List<string> PackageNames = new List<string>();

        public AssetBundleEdge InEdge; // 引用该资源的节点，只体现出一层依赖关系
        public AssetBundleEdge OutEdge;   // 该资源引用的节点，只体现出一层依赖关系
    }

    // 本次打包所有AssetBundle的输出路径，应包含主包包名，以适配增量更新
    public static string AssetBundleOutputPath;

    // 代表了整个打包文件的输出路径
    public static string BuildOutputPath;

    public static AssetManagerConfigScriptableObject AssetManagerConfig;
    public static AssetManagerEditorWindowConfig AssetManagerWindowConfig;

    // 在工具栏中打包AB包，该打包方法必须在Unity中提前设置好要打包的包
    [MenuItem(nameof(AssetManagerEditor) + "/" + nameof(BuildAssetBundle))]
    static void BuildAssetBundle()
    {
        CheckBuildOutputPath();
        
        // Directory类用于PC平台对目录进行操作的类
        if (!Directory.Exists(AssetBundleOutputPath))
        {
            Directory.CreateDirectory(AssetBundleOutputPath);
        }

        // 不同平台之间的AssetBundle不可以通用，该方法会打包工程内所有配置了包名的AB，参数：输出目录，打包格式，打包平台
        BuildPipeline.BuildAssetBundles(AssetBundleOutputPath, BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows);
        AssetDatabase.Refresh(); // 刷新
        Debug.Log($"AB包打包完成，储存路径为：{AssetBundleOutputPath}");
    }

    // 利用MenuItem特性声明一个静态方法,以创建Window类
    [MenuItem("AssetManagerEditor/OpenAssetManagerWindow")]
    static void OpenAssetManagerEditorWindow()
    {
        // 点击时执行该方法(不用绑定),创建一个窗口，设置一个名为AssetManagerName的窗口
        AssetManagerEditorWindow window = EditorWindow.GetWindow<AssetManagerEditorWindow>("AssetManager");
    }

    // 通过静态方法来创建ScriptableObject文件(在工具栏中点击创建)
    [MenuItem("AssetManagerEditor/CreateConfigFile")] 
    public static void CreateConfigScriptableObject()
    {
        // 声明ScriptableObject 类型的实例，就类似与JSON中将某个类的实例化的过程
        AssetManagerConfigScriptableObject assetManagerConfig = ScriptableObject.CreateInstance<AssetManagerConfigScriptableObject>();
        AssetDatabase.CreateAsset(assetManagerConfig, "Assets/Editor/AssetManagerConfig.asset");
        AssetDatabase.SaveAssets();  // 保存资源
        AssetDatabase.Refresh(); // 刷新本地目录
    }

    // 检查是哪个打包模式，设置在窗口中打包AB包的路径
    static void CheckBuildOutputPath()
    {
        switch (AssetManagerConfig.BuildingPattern)
        {
            case AssetBundlePattern.EditorSimulation:
                // 编辑器模式下，不进行打包
                break;
            case AssetBundlePattern.Local:
                // 本地模式，打包到StreamingAssets
                BuildOutputPath = Path.Combine(Application.streamingAssetsPath, "BuildOutput");
                break;
            case AssetBundlePattern.Remote:
                // 远端模式，打包到任意远端路径(在C盘中)
                BuildOutputPath = Path.Combine(Application.persistentDataPath, "BuildOutput");
                break;
        }

        if (!Directory.Exists(BuildOutputPath))
        {
            Directory.CreateDirectory(BuildOutputPath);
        }

        // 在窗口中打包的路径：.../BuildOutput/LocalAssets
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

    // 构建有向图，其实allNodes 可以用成员变量来储存，就不需要再函数中传递了
    static void BuildDirectedGraph(AssetBundleNode lastNode, List<AssetBundleNode> allNodes)
    {
        if (lastNode == null)
        {
            Debug.Log("lastNode为空，构建有向图失败");
        }
        // 只获取直接的依赖
        string[] depends = AssetDatabase.GetDependencies(lastNode.AssetName, false);

        // 依赖的资源数量等于0，代表已经走到了引用关系的最终点，也就是有向图的终点，于是向上返回
        if (depends.Length <= 0)
        {
            return;
        }

        // OutEdge为空代表没有新增过依赖，但是depends > 0 代表肯定有依赖的资源
        if (lastNode.OutEdge == null)
        {
            lastNode.OutEdge = new AssetBundleEdge();
        }

        // 一个资源所直接依赖的资源是有限的，视为x
        foreach (var dependName in depends)
        {

            // 每一个有效的资源都作为一个新的Node
            AssetBundleNode currentNode = null;

            // 而已经存在的Node是一个比较大的值，视为n；有向图构建过程中，嵌套层数为 y
            // 最大的可能的遍历次数为 y*x*n，所以实际上的遍历次数肯定低于n*n
            foreach (AssetBundleNode existingNode in allNodes)
            {
                // 如果资源已经创建节点，那么直接引用已有节点
                if (existingNode.AssetName == dependName)
                {
                    currentNode = existingNode;
                    break;
                }
            }

            // 如果不是已有节点，则新增一个新节点
            if (currentNode == null)
            {
                currentNode = new AssetBundleNode();
                currentNode.AssetName = dependName;

                // 因为当前Node必定是LastNode的依赖对象，所以必然存在InEdge和SourceIndices
                currentNode.InEdge = new AssetBundleEdge();
                currentNode.SourceIndices = new List<int>();
                allNodes.Add(currentNode);
            }

            currentNode.InEdge.Nodes.Add(lastNode);
            lastNode.OutEdge.Nodes.Add(currentNode);

            // 包名以及包对资源的引用，同样也通过有向图进行传递
            if (!string.IsNullOrEmpty(lastNode.PackageName))
            {
                if (!currentNode.PackageNames.Contains(lastNode.PackageName))
                {
                    currentNode.PackageNames.Add(lastNode.PackageName);
                }

            }
            else // 否则是DerivedAsset,直接获取lastNOde的SourceIndices即可
            {
                foreach (string packageNames in lastNode.PackageNames)
                {
                    if (!currentNode.PackageNames.Contains(packageNames))
                    {
                        currentNode.PackageNames.Add(packageNames);
                    }
                }
            }

            // 如果lastNode是SourceAsset，则直接为当前的Node添加lastNode的Index
            // 因为List是一个引用类型，所有SourceAsset的SourceIndices哪怕和内容和Drived一样，也视为一个新的List
            if (lastNode.SourceIndex >= 0)
            {
                if (!currentNode.SourceIndices.Contains(lastNode.SourceIndex))
                {
                    currentNode.SourceIndices.Add(lastNode.SourceIndex);
                }
                
            }
            else // DerivedAsset,直接获取lastNOde的SourceIndices即可
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
    
    // 从有向图中构建AB包
    public static void BuildAssetBundleFromDirectedGraph()
    {
        CheckBuildOutputPath();

        List<AssetBundleNode> allNodes = new List<AssetBundleNode>();
        int sourceIndex = 0;
        Dictionary<string, PackageBuildInfo> packageInfoDic = new Dictionary<string, PackageBuildInfo>();
        
        #region 有向图构建

        for (int i = 0; i < AssetManagerConfig.packageInfoEditors.Count; i++)
        {
            PackageBuildInfo packageBuildInfo = new PackageBuildInfo();
            packageBuildInfo.PackageName = AssetManagerConfig.packageInfoEditors[i].PackageName;
            packageBuildInfo.IsSourcePackage = true;
            packageInfoDic.Add(packageBuildInfo.PackageName, packageBuildInfo);

            // 当前所选中的资源，就是SourceAsset,所以首先添加SourceAsset的Node
            foreach(UnityEngine.Object asset in AssetManagerConfig.packageInfoEditors[i].AssetList)
            {
                AssetBundleNode currentNode = null;

                // 以资源的具体路径，作为资源名称
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

                    // 为什么SourceAsset具有了SourceIndex还需要使用SourceIndices？
                    // 这是因为可以使得OutEdge直接使用SourceAsset的SourceIndices
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

        #region 有向图分区打包
        // key代表SourceIndices，key相同的Node，应该添加到同一个集合中
        Dictionary<List<int>, List<AssetBundleNode>> assetBundleNodesDic = new Dictionary<List<int>, List<AssetBundleNode>>();

        foreach(AssetBundleNode node in allNodes)
        {
            StringBuilder packageNameString = new StringBuilder();

            // 包名不为空或无，则代表是一个SourceAsset，其包名已经在编辑器窗口中添加了
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

                // 此时只添加了对应的包以及包名，而没有具体添加包中对应的Asset
                // 因为Asset添加是需要具有AssetBundleName，所有只能在生成AssetBundleBuild的地方添加Asset
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

            // 遍历所有的key，通过这样的方式就能确保，不同的List之间，内容是一致的
            foreach(List<int> key in assetBundleNodesDic.Keys)
            {
                // 判断key的长度是否和当前node的SourceIndices的长度相等
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

            // Node在构建时就能保证肯定不会重复
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
                //Debug.Log($"Key值的长度={key.Count}，具有Node：{node.AssetName}");

                // 如果一个SourceAsset，则它的PackageName只会具有自己
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

            // 传入的参数是每一个AssetBundle中所有参与打包的Asset路径
            // 如果参与打包的Asset路径没有发生改变，则代表该包没有更新内容
            assetBundleBuilds[buildIndex].assetBundleName = ComputeAssetSetSignature(assetNames);
            assetBundleBuilds[buildIndex].assetNames = assetNames.ToArray();

            foreach (AssetBundleNode node in assetBundleNodesDic[key])
            {

                // 因为区分了的DerivedPackage，所以此处可以确保，每一个Node都具有一个包名
                AssetBuildInfo assetBuildInfo = new AssetBuildInfo();

                assetBuildInfo.AssetName = node.AssetName;
                assetBuildInfo.AssetBundleName = assetBundleBuilds[buildIndex].assetBundleName;

                packageInfoDic[node.PackageName].AssetInfos.Add(assetBuildInfo);

            }
            buildIndex++;
        }

        BuildPipeline.BuildAssetBundles(AssetBundleOutputPath, assetBundleBuilds, CheckIncrementalMode(), BuildTarget.StandaloneWindows);
        Debug.Log($"AB包打包完成，储存路径为：{AssetBundleOutputPath}");

        // 将版本号(数字)写入文件
        string buildVersionFilePath = Path.Combine(BuildOutputPath, "BuildVersion.version");
        File.WriteAllText(buildVersionFilePath, AssetManagerConfig.CurrentBuildVersion.ToString());

        // 创建版本路径，文件夹名即为版本号
        string versionPath = Path.Combine(BuildOutputPath, AssetManagerConfig.CurrentBuildVersion.ToString());
        if (!Directory.Exists(versionPath))
        {
            Directory.CreateDirectory(versionPath);
        }

        BuildAssetBundleHashTable(assetBundleBuilds,versionPath); // 创建hash表

        CopyAssetBundleToVersionFolder(versionPath); // 从主包中复制一份到版本号(文件夹名)中

        BuildPackageTable(packageInfoDic, versionPath); // 创建Package信息

        CreateBuildInfo(versionPath); // 创建BuildInfo信息

        AssetManagerConfig.CurrentBuildVersion++;

        AssetDatabase.Refresh();
    }

    // 计算整个AssetBundle中所有Asset，将AssetGUID转换成byte[]
    static string ComputeAssetSetSignature(IEnumerable<string> assetNames)
    {
        var assetGuids = assetNames.Select(AssetDatabase.AssetPathToGUID);
        MD5 md5 = MD5.Create();

        // 如果传入的asset数量和路径都相同，那么可以得到相同的MD5哈希值
        foreach(string assetGuid in assetGuids.OrderBy(x => x))
        {
            byte[] buffer = Encoding.ASCII.GetBytes(assetGuid);
            md5.TransformBlock(buffer, 0, buffer.Length, null, 0);
        }

        md5.TransformFinalBlock(new byte[0], 0, 0);
        return BytesToHexString(md5.Hash);
    }

    // byte转16进制字符串
    static string BytesToHexString(byte[] bytes)
    {
        StringBuilder byteString = new StringBuilder();
        foreach (byte aByte in bytes)
        {
            byteString.Append(aByte.ToString("x2"));
        }
        return byteString.ToString();
    }

    // 构建AB包的hash表，即记录AB包的大小和hash值
    static string[] BuildAssetBundleHashTable(AssetBundleBuild[] assetBundleBuilds, string versionPath)
    {
        // 表的长度与AssetBundle的数量保持一致
        string[] assetBundleHashs = new string[assetBundleBuilds.Length];
        for(int i = 0; i < assetBundleBuilds.Length; i++)
        {
            string assetBundlePath = Path.Combine(AssetBundleOutputPath, assetBundleBuilds[i].assetBundleName);
           
            FileInfo fileInfo = new FileInfo(assetBundlePath);

            // 表中记录的，是一个AssetBundle文件的长度，以及其内容的MD5哈希值
            assetBundleHashs[i] = $"{fileInfo.Length}_{assetBundleBuilds[i].assetBundleName}";

        }

        // 写入文件
        string hashString = JsonConvert.SerializeObject(assetBundleHashs);
        string hashFilePath = Path.Combine(AssetBundleOutputPath, "AssetBundleHashs");
        string hashFileVersionPath = Path.Combine(versionPath, "AssetBundleHashs");
        File.WriteAllText(hashFilePath, hashString);
        File.WriteAllText(hashFileVersionPath, hashString);
        return assetBundleHashs; 
    }

    // 读取对应路径下的hash表
    static string[] ReadAssetBundleHashTable(string outputPath)
    {
        string hashTablePath = Path.Combine(outputPath, "AssetBundleHashs");
        string hashString = File.ReadAllText(hashTablePath);
        string[] assetHashs = JsonConvert.DeserializeObject<string[]>(hashString);
        return assetHashs;
    }
    static void CopyAssetBundleToVersionFolder(string versionPath)
    {
        // 从AssetBundle输出路径下读取hash表
        string[] assetNames = ReadAssetBundleHashTable(AssetBundleOutputPath);

        // 复制主包
        string mainBundleOriginPath = Path.Combine(AssetBundleOutputPath, "LocalAssets");
        string mainBundleVersionPath = Path.Combine(versionPath, "LocalAssets");

        //str_1为要复制的路径以及文件名。
        //str_2为要粘贴的路径以及文件名。可以不为相同的文件名称，但是后缀必须相同。
        // 复制 LocalAssets 文件
        File.Copy(mainBundleOriginPath, mainBundleVersionPath, true);

        // 将hash表中的每一个都复制一份
        foreach (var assetName in assetNames)
        {
            string assetHashName = assetName.Substring(assetName.IndexOf("_") + 1);

            string assetOriginPath = Path.Combine(AssetBundleOutputPath, assetHashName);
 
            string assetVersionPath = Path.Combine(versionPath, assetHashName);

            File.Copy(assetOriginPath, assetVersionPath, true); // 加一个true，表示覆盖
        }
    }

    // 将ScriptableObject的内容保存为Json格式
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

    // 先加载Config文件
    public static void LoadAssetManagerConfig(AssetManagerEditorWindow window)
    {
        if (AssetManagerConfig == null)
        {
            // 使用AssetDataBase加载资源，只需要传入Assets目录下的路径即可
            AssetManagerConfig = AssetDatabase.LoadAssetAtPath<AssetManagerConfigScriptableObject>(
                                                                    "Assets/Editor/AssetManagerConfig.asset");
            window.VersionString = AssetManagerConfig.AssetManagerVersion.ToString();
            for (int i = window.VersionString.Length - 1; i >= 1; i--)
            {
                window.VersionString = window.VersionString.Insert(i, ".");
            }
        }
    }

    // 再从Json中读取Config，读取后，窗口中的内容和Json中的一致
    public static void ReadConfigFromJSON()
    {
        string configPath = Path.Combine(Application.dataPath, "Editor/AssetManagerConfig.json");
        string configString = File.ReadAllText(configPath);
        JsonUtility.FromJsonOverwrite(configString, AssetManagerConfig);
    }

    // 窗口显示，加载WindowConfig文件
    public static void LoadAssetManagerWindowConfig(AssetManagerEditorWindow window)
    {
        if (window.WindowConfig == null)
        {
            // 使用AssetDataBase加载资源，只需要传入Assets目录下的路径即可
            window.WindowConfig = AssetDatabase.LoadAssetAtPath<AssetManagerEditorWindowConfig>(
                                                              "Assets/Editor/AssetManagerWindowConfig.asset");

            #region 标题LOGO
            window.WindowConfig.LogoTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Images/1.jpg");
            window.WindowConfig.LogoTextureStyle = new GUIStyle();
            window.WindowConfig.LogoTextureStyle.fixedWidth = window.WindowConfig.LogoTexture.width / 2;
            window.WindowConfig.LogoTextureStyle.fixedHeight = window.WindowConfig.LogoTexture.height / 2;
            #endregion

            #region 标题
            window.WindowConfig.TitleTextStyle = new GUIStyle();
            window.WindowConfig.TitleTextStyle.fontSize = 24;
            window.WindowConfig.TitleTextStyle.normal.textColor = Color.red;
            window.WindowConfig.TitleTextStyle.alignment = TextAnchor.MiddleCenter;
            #endregion

            #region 版本号
            window.WindowConfig.VersionTextStyle = new GUIStyle();
            window.WindowConfig.VersionTextStyle.fontSize = 16;
            window.WindowConfig.VersionTextStyle.normal.textColor = Color.green;
            window.WindowConfig.VersionTextStyle.alignment = TextAnchor.MiddleCenter;
            #endregion
        }
    }

    // 在窗口中增加一个Package
    public static void AddPackageInfoEditor()
    {
        AssetManagerConfig.packageInfoEditors.Add(new PackageEditorInfo());
    }

    // 在窗口中移除一个Package
    public static void RemovePackageInfoEditors(PackageEditorInfo info)
    {
        if (AssetManagerConfig.packageInfoEditors.Contains(info))
        {
            AssetManagerConfig.packageInfoEditors.Remove(info);
        }
    }

    // 在窗口中增加一个Asset
    public static void AddAsset(PackageEditorInfo info)
    {
        info.AssetList.Add(null);
    }

    // 在窗口中移除一个Asset
    public static void RemoveAsset(PackageEditorInfo info,UnityEngine.Object asset)
    {
        if (info.AssetList.Contains(asset))
        {
            info.AssetList.Remove(asset);
        }
    }

    public static string PackageTableName = "AllPackages"; // 记录所有的包的名称

    // 创建Package列表
    static void BuildPackageTable(Dictionary<string,PackageBuildInfo> packages, string versionPath)
    {
        string packasgesPath = Path.Combine(AssetBundleOutputPath, PackageTableName);
        string packagesVersionPath = Path.Combine(versionPath, PackageTableName);

        // Package字典，key为包名，将包名写入文件
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

    // 创建BuildInfo文件
    public static void CreateBuildInfo(string versionPath)
    {
        BuildInfos currentBuildInfo = new BuildInfos();
        currentBuildInfo.BuildVersion = AssetManagerConfig.CurrentBuildVersion;

        // 获取AB包输出路径的文件夹信息
        DirectoryInfo directoryInfo = new DirectoryInfo(AssetBundleOutputPath);

        // 获取该文件夹下所有的文件信息
        FileInfo[] fileInfos = directoryInfo.GetFiles();

        // 遍历该文件夹下所有文件，并收集所有文件的长度
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

