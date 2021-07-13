using System.Text.Json.Serialization;

namespace Kurome
{
    public class FileNode
    {
        [JsonPropertyName("fileName")]
        public string FileName { get; set; }
        [JsonPropertyName("isDirectory")]
        public bool IsDirectory { get; set; }
        [JsonPropertyName("size")]
        public long Size { get; set; }
    }
}