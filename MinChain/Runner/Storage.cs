using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace MinChain
{
    public class Storage
    {
        public string BaseLocation { get; }

        DirectoryInfo directory;

        public Storage(string baseLocation)
        {
            directory = new DirectoryInfo(baseLocation);
            BaseLocation = directory.FullName;

            if (!directory.Exists)
                directory.Create();
        }

        public IEnumerable<(ByteString, byte[])> LoadAll()
        {
            var regex = new Regex("^[0-9a-fA-F]{64}$", RegexOptions.Singleline);

            foreach (var file in directory.GetFiles())
            {
                var name = file.Name;
                if (!regex.IsMatch(name)) continue;

                var id = ByteString.CopyFrom(HexConvert.ToBytes(name));
                var bytes = File.ReadAllBytes(file.FullName);

                yield return (id, bytes);
            }
        }

        public void Save(ByteString id, byte[] bytes)
        {
            if (id.IsNull())
                throw new ArgumentNullException(nameof(id));

            if (id.Length != 32)
                throw new ArgumentException(nameof(id));

            if (bytes.IsNull())
                throw new ArgumentException(nameof(bytes));

            var fileName = Path.Combine(
                directory.FullName,
                HexConvert.FromBytes(id.ToByteArray()));
            var fileInfo = new FileInfo(fileName);

            // We assume the hash never collides.
            if (fileInfo.Exists) return;

            using (var stream = fileInfo.OpenWrite())
                stream.Write(bytes, 0, bytes.Length);
        }
    }
}
