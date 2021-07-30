namespace Kurome
{
    public class Packets
    {
        public const byte ResultActionSuccess = 0;
        public const byte ActonGetEnumerateDirectory = 1;
        public const byte ActionGetSpaceInfo = 2;
        public const byte ActionGetFileType = 3;
        public const byte ActionWriteDirectory = 4;
        public const byte ResultFileIsDirectory = 5;
        public const byte ResultFileIsFile = 6;
        public const byte ResultFileNotFound = 7;
        public const byte ActionDelete = 8;
        public const byte ActionSendToServer = 10;
        public const byte ActionGetFileInfo = 11;
    }
}