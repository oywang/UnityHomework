using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "AssetManagerWindowConfig", menuName = "AssetManager/AssetManagerWindowConfig", order = 1)]
public class AssetManagerEditorWindowConfig : ScriptableObject
{
    public GUIStyle TitleTextStyle; // ����������ʽ
    public GUIStyle VersionTextStyle; // �汾����ʽ

    public Texture2D LogoTexture; // LogoͼƬ
    public GUIStyle LogoTextureStyle; // Logo��ʽ
}

