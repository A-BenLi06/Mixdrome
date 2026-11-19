// CLR filesystem adapters for exercising the production phone cache service.
// Native StorageFolder behavior, media decoding, and rendered UI need native QA.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Windows.Storage
{
    public enum CreationCollisionOption { ReplaceExisting, OpenIfExists, FailIfExists }
    public enum NameCollisionOption { ReplaceExisting, FailIfExists }
    public enum StorageDeleteOption { PermanentDelete }
    public sealed class ApplicationData
    {
        public static readonly ApplicationData Current = new ApplicationData();
        public StorageFolder LocalFolder { get; private set; }
        public void UseRoot(string path) { Directory.CreateDirectory(path); LocalFolder = new StorageFolder(path); }
    }
    public sealed class BasicProperties { public ulong Size; }
    public sealed class StorageFolder
    {
        public string Path { get; private set; }
        public string Name { get { return System.IO.Path.GetFileName(Path); } }
        public StorageFolder(string path) { Path = System.IO.Path.GetFullPath(path); }
        internal string Child(string name)
        {
            var resolved = System.IO.Path.GetFullPath(System.IO.Path.Combine(Path, name));
            if (!resolved.StartsWith(Path + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || System.IO.Path.GetFileName(name) != name)
                throw new InvalidDataException("Test adapter rejected a path outside its folder.");
            return resolved;
        }
        public Task<StorageFolder> CreateFolderAsync(string name, CreationCollisionOption collision)
        {
            var path = Child(name);
            if (collision == CreationCollisionOption.FailIfExists && Directory.Exists(path)) throw new IOException("Folder exists.");
            Directory.CreateDirectory(path); return Task.FromResult(new StorageFolder(path));
        }
        public Task<StorageFolder> GetFolderAsync(string name)
        { var path = Child(name); if (!Directory.Exists(path)) throw new FileNotFoundException(); return Task.FromResult(new StorageFolder(path)); }
        public Task<IReadOnlyList<StorageFolder>> GetFoldersAsync()
        { return Task.FromResult<IReadOnlyList<StorageFolder>>(Directory.GetDirectories(Path).Select(path => new StorageFolder(path)).ToList()); }
        public Task<IReadOnlyList<StorageFile>> GetFilesAsync()
        { return Task.FromResult<IReadOnlyList<StorageFile>>(Directory.GetFiles(Path).Select(path => new StorageFile(path)).ToList()); }
        public Task<StorageFile> CreateFileAsync(string name, CreationCollisionOption collision)
        {
            var path = Child(name);
            if (collision == CreationCollisionOption.FailIfExists && File.Exists(path)) throw new IOException("File exists.");
            if (collision == CreationCollisionOption.ReplaceExisting || !File.Exists(path)) File.WriteAllBytes(path, new byte[0]);
            return Task.FromResult(new StorageFile(path));
        }
        public Task<StorageFile> GetFileAsync(string name)
        { var path = Child(name); if (!File.Exists(path)) throw new FileNotFoundException(); return Task.FromResult(new StorageFile(path)); }
        public Task DeleteAsync(StorageDeleteOption option) { Directory.Delete(Path, true); return Task.FromResult(true); }
    }
    public sealed class StorageFile
    {
        public string Path { get; private set; }
        public string Name { get { return System.IO.Path.GetFileName(Path); } }
        public StorageFile(string path) { Path = path; }
        public Task<BasicProperties> GetBasicPropertiesAsync() { return Task.FromResult(new BasicProperties { Size = (ulong)new FileInfo(Path).Length }); }
        public Task<Stream> OpenStreamForWriteAsync() { return Task.FromResult<Stream>(new FileStream(Path, FileMode.Create, FileAccess.Write, FileShare.None)); }
        public Task RenameAsync(string name, NameCollisionOption collision)
        {
            var target = new StorageFolder(System.IO.Path.GetDirectoryName(Path)).Child(name);
            if (File.Exists(target)) { if (collision == NameCollisionOption.FailIfExists) throw new IOException("Target exists."); File.Delete(target); }
            File.Move(Path, target); Path = target; return Task.FromResult(true);
        }
        public Task DeleteAsync(StorageDeleteOption option) { File.Delete(Path); return Task.FromResult(true); }
    }
    public static class FileIO
    {
        public static Task<string> ReadTextAsync(StorageFile file) { return Task.FromResult(File.ReadAllText(file.Path, System.Text.Encoding.UTF8)); }
        public static Task WriteTextAsync(StorageFile file, string text) { File.WriteAllText(file.Path, text, new System.Text.UTF8Encoding(false)); return Task.FromResult(true); }
    }
}
namespace Windows.Security.Cryptography
{
    public enum BinaryStringEncoding { Utf8 }
    public static class CryptographicBuffer
    {
        public static byte[] ConvertStringToBinary(string text, BinaryStringEncoding encoding) { return System.Text.Encoding.UTF8.GetBytes(text); }
        public static string EncodeToHexString(byte[] bytes) { return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant(); }
    }
}
namespace Windows.Security.Cryptography.Core
{
    public static class HashAlgorithmNames { public const string Sha256 = "SHA256"; }
    public sealed class HashAlgorithmProvider
    {
        public static HashAlgorithmProvider OpenAlgorithm(string name) { return new HashAlgorithmProvider(); }
        public byte[] HashData(byte[] bytes) { using (var hash = System.Security.Cryptography.SHA256.Create()) return hash.ComputeHash(bytes); }
    }
}
namespace Windows.ApplicationModel.Resources
{
    public sealed class ResourceLoader
    {
        public static ResourceLoader GetForCurrentView() { return new ResourceLoader(); }
        public string GetString(string key) { return key; }
    }
}
namespace Mixdrome.Phone.Services
{
    public static class PhoneSettings
    {
        public static Client.Common.Services.DataStructures.SubsonicService.SubsonicServiceConfiguration Configuration;
        public static Client.Common.Services.DataStructures.SubsonicService.SubsonicServiceConfiguration Load() { return Configuration; }
    }
    public static class PhoneCollectionPresentation
    { public const string DefaultArtwork = "ms-appx:///Assets/MixRadio/DefaultArtwork300.png"; }
}
namespace Mixdrome.Phone.Views
{
    public sealed class SongDisplayItem
    {
        public string Id { get; set; }
        public int Track { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public int Duration { get; set; }
        public string CoverArt { get; set; }
        public string StreamUrl { get; set; }
    }
}
