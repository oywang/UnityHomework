using UnityEditor;
using UnityEngine;

// ֻ����������Ⱦ���ֵĴ���
public class AssetManagerEditorWindow : EditorWindow
{
    public string VersionString;

    public AssetManagerEditorWindowConfig WindowConfig;

    // Scriptable �����ֵ�����л��ģ�������Editor����������ScriptableObject�����ǻ�
    // ���ű�������Ĭ��ֵ(null)��
    public void Awake()
    {
        AssetManagerEditor.LoadAssetManagerConfig(this);
        AssetManagerEditor.LoadAssetManagerWindowConfig(this);
    }

    // ÿ�����ı�ʱ�����ø÷���
    private void OnValidate()
    {
        AssetManagerEditor.LoadAssetManagerConfig(this);
        AssetManagerEditor.LoadAssetManagerWindowConfig(this);

    }

    private void OnInspectorUpdate()
    {        
        AssetManagerEditor.LoadAssetManagerConfig(this);
        AssetManagerEditor.LoadAssetManagerWindowConfig(this);
    }


    // ���еĽ�����Ⱦ����Ҫͨ������������������ֶ�ָ����Ⱦ��ʽ��
    // ��Ҳ������Unity��дUI�ķ�ʽ��ONGUI��������ÿ����Ⱦ֡�����ã�
    // ��Editor������Ҳ����ˣ�ֻ��������Editor�������Ⱦ֡����Game�Ӵ���
    private void OnGUI()
    {
        // Ĭ��������Ǵ�ֱ�Ű棬GUI���մ���˳�������Ⱦ
        #region ����ͼ
        GUILayout.Space(20);// �̶��ߴ�Ŀհ�����
        if(WindowConfig.LogoTexture != null)
        {
            GUILayout.Label(WindowConfig.LogoTexture, WindowConfig.LogoTextureStyle);
        }
        
        #endregion

        #region ����
        GUILayout.Space(20); // GUILayout.Label()�Ὣ���ݺ���ʽ��ʾ�ڴ�����
        GUILayout.Label(nameof(AssetManagerEditor), WindowConfig.TitleTextStyle); // �Դ��Ű湦�ܵ�GUI����
        #endregion

        #region �汾��
        GUILayout.Space(20);
        GUILayout.Label(VersionString, WindowConfig.VersionTextStyle);
        #endregion

        #region ���ģʽѡ��
        GUILayout.Space(20);
        // �ڴ����д���һ�����ģʽ��ѡ��
        AssetManagerEditor.AssetManagerConfig.BuildingPattern = (AssetBundlePattern)EditorGUILayout.EnumPopup("���ģʽ",
            AssetManagerEditor.AssetManagerConfig.BuildingPattern);
        #endregion

        #region ѹ��ģʽѡ��
        GUILayout.Space(20);
        AssetManagerEditor.AssetManagerConfig.CompressionPattern = (AssetBundleCompressionPattern)EditorGUILayout.EnumPopup("ѹ����ʽ",
            AssetManagerEditor.AssetManagerConfig.CompressionPattern);
        #endregion

        #region �������ѡ��
        GUILayout.Space(20);
        AssetManagerEditor.AssetManagerConfig.BuildMode = (IncrementalBuildMode)EditorGUILayout.EnumPopup("�������ģʽ",
            AssetManagerEditor.AssetManagerConfig.BuildMode);
        #endregion

        #region �����Դѡ��
        GUILayout.BeginVertical("frameBox");
        GUILayout.Space(10);
        for(int i=0;i< AssetManagerEditor.AssetManagerConfig.packageInfoEditors.Count; i++)
        {
            PackageEditorInfo packageInfo = AssetManagerEditor.AssetManagerConfig.packageInfoEditors[i];
            GUILayout.BeginVertical("frameBox");
            GUILayout.BeginHorizontal();
            packageInfo.PackageName = EditorGUILayout.TextField("PackageName", packageInfo.PackageName);

            if (GUILayout.Button("Remove"))
            {
                AssetManagerEditor.RemovePackageInfoEditors(packageInfo);
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(10);
            for (int j = 0; j < packageInfo.AssetList.Count; j++)
            {
                GUILayout.BeginHorizontal();
                packageInfo.AssetList[j] = EditorGUILayout.ObjectField(packageInfo.AssetList[j], typeof(GameObject)) as GameObject;

                if (GUILayout.Button("Remove"))
                {
                    AssetManagerEditor.RemoveAsset(packageInfo,packageInfo.AssetList[j]);
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(5);
            }
            if (GUILayout.Button("����Asset"))
            {
                AssetManagerEditor.AddAsset(packageInfo);
            }
            GUILayout.EndVertical();
            GUILayout.Space(10);
        }
        GUILayout.Space(10);
        if (GUILayout.Button("����Package"))
        {
            AssetManagerEditor.AddPackageInfoEditor();
        }
        GUILayout.EndVertical();
        #endregion

        #region �����ť
        GUILayout.Space(20);

        // �ڴ����д���һ��Button����ʾ����Ϊ�����AssetBundle
        if (GUILayout.Button("���AssetBundle"))
        {

            AssetManagerEditor.BuildAssetBundleFromDirectedGraph();
        }
        #endregion

        #region ���桢��ȡConfig
        GUILayout.Space(10);
        if (GUILayout.Button("����Config�ļ�"))
        {
            AssetManagerEditor.SaveConfigToJSON();
        }

        GUILayout.Space(10);
        if (GUILayout.Button("��ȡConfig�ļ�"))
        {
            AssetManagerEditor.ReadConfigFromJSON();
        }
        #endregion   
    }
}
