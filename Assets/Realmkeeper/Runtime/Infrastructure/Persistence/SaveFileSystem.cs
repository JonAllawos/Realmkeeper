using System.IO;
using System.Text;

namespace Realmkeeper.Infrastructure.Persistence
{
    /// <summary>
    /// M1.1: the file operations a save uses, as a seam. The default writes to disk; tests substitute one that fails part
    /// way, truncates or is denied, to prove an old save and its backup always survive.
    /// </summary>
    public interface ISaveFileSystem
    {
        void WriteAllText(string path, string text);
        string ReadAllText(string path);

        /// <summary>Atomically replaces <paramref name="destination"/> with <paramref name="source"/>, keeping the old one as <paramref name="backup"/>.</summary>
        void Replace(string source, string destination, string backup);

        void Move(string source, string destination);
    }

    public sealed class SaveFileSystem : ISaveFileSystem
    {
        public static readonly SaveFileSystem Instance = new SaveFileSystem();

        public void WriteAllText(string path, string text) => File.WriteAllText(path, text, new UTF8Encoding(false));
        public string ReadAllText(string path) => File.ReadAllText(path, Encoding.UTF8);
        public void Replace(string source, string destination, string backup) => File.Replace(source, destination, backup);
        public void Move(string source, string destination) => File.Move(source, destination);
    }
}
