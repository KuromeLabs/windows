namespace Kurome
{
    public static class IdentityProvider
    {
        public static string GetMachineName()
        {
            return Environment.MachineName;
        }

        public static string GetGuid()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Worker"
            );
            var file = Path.Combine(dir, "id");
            if (File.Exists(file))
            {
                using var fs = File.OpenText(file);
                return fs.ReadLine();
            }
            else
            {
                Directory.CreateDirectory(dir);
                using var fs = File.CreateText(file);
                var guid = Guid.NewGuid().ToString();
                fs.Write(guid);
                return guid;
            }
        }
    }
}