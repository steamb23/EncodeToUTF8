using System.Text;

namespace SteamB23.EncodeToUTF8
{
    public class FileData
    {
        public FileData(string filePath, Encoding encoding, bool hasBom)
        {
            FilePath = filePath;
            Encoding = encoding;
            HasBOM = hasBom;
        }

        public string FilePath { get; set; }
        public Encoding Encoding { get; set; }
        public bool HasBOM { get; set; }

        public FileData Clone()
        {
            return (MemberwiseClone() as FileData)!;
        }
    }
}