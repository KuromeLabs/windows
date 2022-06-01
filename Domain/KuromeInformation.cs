namespace Domain;

public class KuromeInformation
{
    //copy constructor
    public KuromeInformation(KuromeInformation kuromeInformation)
    {
        FileName = kuromeInformation.FileName;
        CreationTime = kuromeInformation.CreationTime;
        LastAccessTime = kuromeInformation.LastAccessTime;
        LastWriteTime = kuromeInformation.LastWriteTime;
        Length = kuromeInformation.Length;
        IsDirectory = kuromeInformation.IsDirectory;
    }

    public KuromeInformation()
    {
    }

    public string FileName { get; set; } = null!;
    public DateTime? CreationTime { get; set; }
    public DateTime? LastAccessTime { get; set; }
    public DateTime? LastWriteTime { get; set; }
    public long Length { get; set; }
    public bool IsDirectory { get; set; }
}