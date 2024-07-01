using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;



// fileName = �����ť֮�󴴽���Ĭ���ļ�����menuName = ��Create�˵��еĲ˵��㼶
// order = �涨�˴��� ScriptableObject ������Դ�ļ���·���ڲ˵�����ʾ��λ�ã�order Խ�ͣ�����ʾ��������
// ��� order ��ͬ�����ǰ��ռ̳��� ScriptableObject �Ľű�����ʱ�������´�������������

// �Ҽ�������ScriptableObject(�ɱ�̶���)
[CreateAssetMenu(fileName ="AssetManagerConfig",menuName ="AssetManager/AssetManagerConfig",order =1)]
public class AssetManagerConfigScriptableObject : ScriptableObject
{
   // ���ģʽѡ��
    public AssetBundlePattern BuildingPattern;

    // ���ѹ��ģʽѡ��
    public AssetBundleCompressionPattern CompressionPattern;

    // ���ģʽѡ���Ƿ�Ӧ���������
    public IncrementalBuildMode BuildMode;

    // ֮����ʹ����������Ϊ�������
    public int AssetManagerVersion = 100; // ��Դ���������ߵİ汾

    public int CurrentBuildVersion = 100; // AssetBundle������汾

    public string[] InvalidExtentionName = new string[] { ".meta", ".cs" };

    public List<PackageEditorInfo> packageInfoEditors = new List<PackageEditorInfo>();

}


