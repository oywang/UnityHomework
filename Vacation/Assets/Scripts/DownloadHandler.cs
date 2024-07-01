using System.Collections;
using System.Collections.Generic;
using System.IO;  // ��дDownloadHandle
using UnityEngine;
using UnityEngine.Networking;

// ���屨�����͵�ö��
public enum ErrorCode 
{ 
    DownloadFileEmpty, // ��Ҫ������Դ����Ϊ��

    TempFileMissing  // ��ʱ�ļ���ʧ
}

// �޲Σ��޷���ֵ��ί��
// ί����ʵ�ʣ���������һ���ض�����ֵ���ض������ĺ�������������ָ��ĳһ�����κη��Ϲ���ĺ���
// ��������ĳ��ί�У��κη��Ϲ���ĺ�����������ί�и�ĳ��ί��ʵ��(ί�б���)�����á�
// ��ν�����Ĺ�����ʵ����һ��������ʲô���͵ķ���ֵ���;����ļ��������涨

// ���ش���ʱ�ص�
public delegate void ErrorEventHander(ErrorCode errorCode, string messge);

// �������ʱ�ص�
public delegate void CompletedEventHander(string fileName, string message);

// ���ؽ��ȸ���ʱ�ص�����������ǰ���ȣ���ǰ������ɵĳ��ȣ��ļ��ܳ���
public delegate void ProgressEventHander(float currProgress, long currentLength, long totalLength);
public class DownloadHandler : DownloadHandlerScript
{
    string SavePath;  // ������ɺ󱣴��·��
    string TempPath;  // ������ʱ�ļ��Ĵ���·��
    long currentLength = 0; // ��ǰ�Ѿ����ص��ļ�����
    long totalLength = 0; // �ļ������ݳ���(�ֽڳ���)
    long contentLength = 0;  // ������Ҫ���ص����ݳ���
    FileStream fileSteam = null; // �ļ���д�������������յ�������д���ļ�
    ErrorEventHander OnError = null;  // ����ʱ�Ļص�������ί������
    CompletedEventHander OnCompleted = null;  // �������ʱִ�еĻص�����
    ProgressEventHander OnProgress = null; // ���ؽ��ȸ���ʱִ�еĻص�����

    public long CurrentLength
    {
        get { return currentLength; }
    }

    public long TotalLength
    {
        get { return totalLength; }
    }

    // ͨ�����캯��,����������ֵ,ע��,�˴���������֮������:�﷨��base�ؼ���
    // ����÷����̳��Ը����ͬ������,����byte����ĳ��ȴ�����Ϊ������ط���Ļ����С
    public DownloadHandler(string savePath,CompletedEventHander onCompleted,
        ProgressEventHander onProgress,ErrorEventHander onError) : base(new byte[1024 * 1024])
    {
        // �ڹ��������У�this�ؼ��ִ�����ι��������������Ķ�Ӧ���͵�ʵ��
        this.SavePath = savePath.Replace("\\", "/");

        this.OnCompleted = onCompleted;
        this.OnProgress = onProgress;
        this.OnError = onError;

        // ԭ�����ļ�·���£����ⴴ��һ��.temp�ļ�
        this.TempPath = savePath + ".temp";

        // �ҵ���Ӧ�ļ�·���µ���ʱ�ļ���ʹ�������ļ����ķ�ʽ����
        this.fileSteam = new FileStream(this.TempPath, FileMode.OpenOrCreate, FileAccess.ReadWrite);

        // ����ǰ���ȸ���Ϊ��ʱ�ļ���д����ֽڳ���
        this.currentLength = this.fileSteam.Length;

        // ��������֮�⣬д���ļ�ҲҪ����д����󳤶ȼ�������д��ȥ
        this.fileSteam.Position = this.currentLength;
    }

    // ʹ��overide�ؼ���,��д�����е�ͬ������ʹ���յ�Զ�̷���������ʱ,
    // ʹ��file.Stream����,������д�����ݵ�������ʱ�ļ���,�����µ�ǰ�ѻ������ݵĳ���

    // ������Headerʱ���ø÷��������յ� ContentLength ��ͷ���õĻص�
    // contentLength�����ļ���ĳ���ֽڿ�ʼ���ļ������һ���ֽڵĳ���
    protected override void ReceiveContentLengthHeader(ulong contentLength)
    {
        this.contentLength = (long)contentLength;

        // һ���ļ��ĳ��� = �Ѿ����صĳ��� + δ���صĳ���
        this.totalLength = this.contentLength + currentLength;
    }

    // ��Զ�̷������յ�����ʱ���õĻص�����ÿ�δӷ��������յ���Ϣʱ�����
    protected override bool ReceiveData(byte[] datas, int dataLength)
    {
        // ������ص����ݳ���С��0���ͽ�������
        if(contentLength <= 0 || datas == null || datas.Length <= 0)
        {
            return false;
        }

        // ������0��length����ָdatas��λ��
        this.fileSteam.Write(datas, 0, dataLength);

        currentLength += dataLength;

        // ����1.0f��Ϊ����ʽת����float����
        OnProgress?.Invoke(currentLength * 1.0f / totalLength, currentLength, totalLength);

        return true;
    }

    // ��Զ�˷������յ���������ʱ��������ɣ����õĻص�
    protected override void CompleteContent()
    {
        // �������������ݺ����ȹر��ļ�������
        FileStreamClose();

        // ����������ϲ����ڸ��ļ����������ص����ݻ�Ϊ0��������Ҫ���⴦���������
        if(contentLength <= 0)
        {
            OnError.Invoke(ErrorCode.DownloadFileEmpty, "�������ݳ���Ϊ��0");
            return;
        }

        // ������ص��ļ��Ѿ����ڣ���ɾ��ԭ�ļ�
        if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
        }

        // ͨ�������ϵļ���󣬾ͽ���ʱ�ļ��ƶ���Ŀ��·���£��������
        File.Move(TempPath, SavePath);
        FileInfo fileInfo = new FileInfo(SavePath);
        OnCompleted.Invoke(fileInfo.Name, "�ļ��������");
    }

    public override void Dispose()
    {
        base.Dispose();
        FileStreamClose();
    }

    // �ر��ļ���
    void FileStreamClose()
    {
        if(fileSteam == null)
        {
            return;
        }

        fileSteam.Close();
        fileSteam.Dispose();
        fileSteam = null;
    }
}
