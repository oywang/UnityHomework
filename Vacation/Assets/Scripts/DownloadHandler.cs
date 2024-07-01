using System.Collections;
using System.Collections.Generic;
using System.IO;  // 重写DownloadHandle
using UnityEngine;
using UnityEngine.Networking;

// 定义报错类型的枚举
public enum ErrorCode 
{ 
    DownloadFileEmpty, // 需要下载资源内容为空

    TempFileMissing  // 临时文件丢失
}

// 无参，无返回值的委托
// 委托是实质，就是声明一种特定返回值，特定参数的函数，但不具体指定某一个，任何符合规则的函数
// 都可以是某个委托，任何符合规则的函数，都可以委托给某个委托实例(委托变量)来调用。
// 所谓函数的规则，其实就是一个函数由什么类型的返回值，和具体哪几个参数规定

// 下载错误时回调
public delegate void ErrorEventHander(ErrorCode errorCode, string messge);

// 下载完成时回调
public delegate void CompletedEventHander(string fileName, string message);

// 下载进度更新时回调，参数：当前进度，当前下载完成的长度，文件总长度
public delegate void ProgressEventHander(float currProgress, long currentLength, long totalLength);
public class DownloadHandler : DownloadHandlerScript
{
    string SavePath;  // 下载完成后保存的路经
    string TempPath;  // 下载临时文件的储存路径
    long currentLength = 0; // 当前已经下载的文件长度
    long totalLength = 0; // 文件总数据长度(字节长度)
    long contentLength = 0;  // 本次需要下载的数据长度
    FileStream fileSteam = null; // 文件读写流，用来将接收到的数据写入文件
    ErrorEventHander OnError = null;  // 出错时的回调函数，委托类型
    CompletedEventHander OnCompleted = null;  // 下载完成时执行的回调函数
    ProgressEventHander OnProgress = null; // 下载进度跟新时执行的回调函数

    public long CurrentLength
    {
        get { return currentLength; }
    }

    public long TotalLength
    {
        get { return totalLength; }
    }

    // 通过构造函数,来给变量赋值,注意,此处函数声明之后还用了:语法和base关键字
    // 代表该方法继承自父类的同名方法,其中byte数组的长度代表了为这次下载分配的缓存大小
    public DownloadHandler(string savePath,CompletedEventHander onCompleted,
        ProgressEventHander onProgress,ErrorEventHander onError) : base(new byte[1024 * 1024])
    {
        // 在构建函数中，this关键字代表这次构建过程中声明的对应类型的实例
        this.SavePath = savePath.Replace("\\", "/");

        this.OnCompleted = onCompleted;
        this.OnProgress = onProgress;
        this.OnError = onError;

        // 原本的文件路径下，额外创建一个.temp文件
        this.TempPath = savePath + ".temp";

        // 找到对应文件路径下的临时文件，使用这种文件流的方式访问
        this.fileSteam = new FileStream(this.TempPath, FileMode.OpenOrCreate, FileAccess.ReadWrite);

        // 将当前长度更新为临时文件已写入的字节长度
        this.currentLength = this.fileSteam.Length;

        // 除了下载之外，写入文件也要从已写入最大长度继续往下写下去
        this.fileSteam.Position = this.currentLength;
    }

    // 使用overide关键字,重写父类中的同名方法使得收到远程服务器数据时,
    // 使用file.Stream方法,持续的写入数据到本地临时文件中,并更新当前已缓存数据的长度

    // 当设置Header时调用该方法，在收到 ContentLength 标头调用的回调
    // contentLength：从文件的某个字节开始到文件的最后一个字节的长度
    protected override void ReceiveContentLengthHeader(ulong contentLength)
    {
        this.contentLength = (long)contentLength;

        // 一个文件的长度 = 已经下载的长度 + 未下载的长度
        this.totalLength = this.contentLength + currentLength;
    }

    // 从远程服务器收到数据时调用的回调，在每次从服务器上收到消息时会调用
    protected override bool ReceiveData(byte[] datas, int dataLength)
    {
        // 如果下载的数据长度小于0，就结束下载
        if(contentLength <= 0 || datas == null || datas.Length <= 0)
        {
            return false;
        }

        // 这里是0和length都是指datas的位置
        this.fileSteam.Write(datas, 0, dataLength);

        currentLength += dataLength;

        // 乘以1.0f是为了隐式转换成float类型
        OnProgress?.Invoke(currentLength * 1.0f / totalLength, currentLength, totalLength);

        return true;
    }

    // 从远端服务器收到所有数据时（下载完成）调用的回调
    protected override void CompleteContent()
    {
        // 接收完所有数据后，首先关闭文件流对象
        FileStreamClose();

        // 如果服务器上不存在该文件，请求下载的内容会为0，所有需要特殊处理这种情况
        if(contentLength <= 0)
        {
            OnError.Invoke(ErrorCode.DownloadFileEmpty, "下载内容长度为：0");
            return;
        }

        // 如果下载的文件已经存在，就删除原文件
        if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
        }

        // 通过了以上的检验后，就将临时文件移动到目标路径下，下载完成
        File.Move(TempPath, SavePath);
        FileInfo fileInfo = new FileInfo(SavePath);
        OnCompleted.Invoke(fileInfo.Name, "文件下载完成");
    }

    public override void Dispose()
    {
        base.Dispose();
        FileStreamClose();
    }

    // 关闭文件流
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
