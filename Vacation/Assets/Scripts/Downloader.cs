using System.Collections;
using System.Collections.Generic;
using UnityEngine;  // ʵ��Downloader��������
using UnityEngine.Networking;

public class DownloadedInfos
{
    // ���浱ǰ�Ѿ�������Ϣ���Ѿ����ص��ļ���
    public List<string> DownloadedFileNames = new List<string>();
}

public class Downloader
{
    string URL = null; // ��������ַ����Ҫ���ص��ļ���ַ
    string SavePath = null; // �ļ�����·��
    UnityWebRequest request = null;  // ���������ʵ����Unity��������Web����������ͨ�ŵ���
    DownloadHandler downloadHandler = null;  // �������Լ�ʵ�ֵ����ش�����
    ErrorEventHander OnError; // ����ʱ�ص�
    ProgressEventHander OnProgress; // ����ʱ�ص�
    CompletedEventHander OnCompleted; // ���ʱ�ص�

    // �ļ���URL·�������ص�����savePath
    public Downloader(string url,string savePath,CompletedEventHander onComPleted,
                           ProgressEventHander onProgress,ErrorEventHander onError)
    {
        this.URL = url;
        this.SavePath = savePath;
        this.OnCompleted = onComPleted;
        this.OnProgress = onProgress;
        this.OnError = onError;
    }

    // ��ʼ���غ���
    public void StartDownload()
    {
        request = UnityWebRequest.Get(URL);
        if (!string.IsNullOrEmpty(SavePath))
        {
            // timeoutʱ��Ӧ��������ʱ��������ĳЩʱ�������û��׼�������ݣ�
            // �Լ����ش洢ʱ�䵼������ʱ����ʱ���ᴥ��OnError
            request.timeout = 30; // ����ʱ������Ϊ30��
            request.disposeDownloadHandlerOnDispose = true;

            // downloadHander�������Լ���д���࣬���е������ʵ����ʱ���Զ�����һ��FileStream��
            // ����ȡ��ʱ�ļ����ֽڳ��ȣ���Ϊ�����������ʼλ��
            downloadHandler = new DownloadHandler(SavePath, OnCompleted, OnProgress, OnError);

            // ����������HTTP������ͷ��range��ʾ����Դ�Ĳ�������(��������Ӧͷ�Ĵ�С)����λ��byte
            // ��ΪcurrentLength����ʵ�����Լ��յ�����������ʱ���£�����ʼ�տ��Ա�ﱾ���ļ��ĳ���
            request.SetRequestHeader("range", $"bytes = {downloadHandler.CurrentLength}-");

            request.downloadHandler = downloadHandler;
        }
        request.SendWebRequest();
    }

    // ���������ͷŷ�������������ɺ󣬿��Ե��ø÷����ͷ�request
    public void Dispose()
    {
        OnError = null;
        OnCompleted = null;
        OnProgress = null;
        if(request != null)
        {
            // �������û����ɾ���ֹ
            if (!request.isDone)
            {
                // ������������
                request.Abort();
            }
            request.Dispose();
            request = null;
        }
    }
}
