using System.Collections;
using System.Collections.Generic;
using UnityEngine;  // 实现Downloader下载器类
using UnityEngine.Networking;

public class DownloadedInfos
{
    // 储存当前已经下载信息，已经下载的文件名
    public List<string> DownloadedFileNames = new List<string>();
}

public class Downloader
{
    string URL = null; // 服务器地址，需要下载的文件地址
    string SavePath = null; // 文件保存路径
    UnityWebRequest request = null;  // 具体的下载实例，Unity中用来与Web服务器进行通信的类
    DownloadHandler downloadHandler = null;  // 由我们自己实现的下载处理类
    ErrorEventHander OnError; // 出错时回调
    ProgressEventHander OnProgress; // 进行时回调
    CompletedEventHander OnCompleted; // 完成时回调

    // 文件从URL路径中下载到本地savePath
    public Downloader(string url,string savePath,CompletedEventHander onComPleted,
                           ProgressEventHander onProgress,ErrorEventHander onError)
    {
        this.URL = url;
        this.SavePath = savePath;
        this.OnCompleted = onComPleted;
        this.OnProgress = onProgress;
        this.OnError = onError;
    }

    // 开始下载函数
    public void StartDownload()
    {
        request = UnityWebRequest.Get(URL);
        if (!string.IsNullOrEmpty(SavePath))
        {
            // timeout时长应大于下载时长，避免某些时候服务器没有准备好数据，
            // 以及本地存储时间导致请求超时，超时不会触发OnError
            request.timeout = 30; // 请求时间上限为30秒
            request.disposeDownloadHandlerOnDispose = true;

            // downloadHander是我们自己重写的类，所有当这个类实例化时会自动开启一个FileStream，
            // 来读取临时文件的字节长度，作为本次请求的起始位置
            downloadHandler = new DownloadHandler(SavePath, OnCompleted, OnProgress, OnError);

            // 这里是设置HTTP的请求头，range表示求资源的部分内容(不包括响应头的大小)，单位：byte
            // 因为currentLength会在实例化以及收到服务器数据时更新，所以始终可以表达本地文件的长度
            request.SetRequestHeader("range", $"bytes = {downloadHandler.CurrentLength}-");

            request.downloadHandler = downloadHandler;
        }
        request.SendWebRequest();
    }

    // 下载器的释放方法，当下载完成后，可以调用该方法释放request
    public void Dispose()
    {
        OnError = null;
        OnCompleted = null;
        OnProgress = null;
        if(request != null)
        {
            // 如果下载没有完成就终止
            if (!request.isDone)
            {
                // 放弃本次请求
                request.Abort();
            }
            request.Dispose();
            request = null;
        }
    }
}
