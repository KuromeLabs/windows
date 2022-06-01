using Application.Interfaces;

namespace Infrastructure.Devices
{
    public class IdentityProvider : IIdentityProvider
    {
        private string? _id;
        private string? Name;

        public string GetEnvironmentName()
        {
            return Name ??= Environment.MachineName;
        }

        public string GetEnvironmentId()
        {
            if (_id != null) return _id;
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Kurome"
            );
            var file = Path.Combine(dir, "id");
            if (File.Exists(file))
            {
                using var fs = File.OpenText(file);
                _id = fs.ReadLine()!;
            }
            else
            {
                Directory.CreateDirectory(dir);
                using var fs = File.CreateText(file);
                var guid = Guid.NewGuid().ToString();
                fs.Write(guid);
                _id = guid;
            }
            return _id;
        }
    }
}