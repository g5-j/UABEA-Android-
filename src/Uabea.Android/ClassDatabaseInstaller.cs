using Android.Content.Res;
using System.Security.Cryptography;

namespace Uabea.Android;

static class ClassDatabaseInstaller
{
    const string Expected = "DCEA167EB5E8D17E7AA33C933A7332E10F88642F7E6E791B0CC2A6B7F44EC247";
    static readonly object Sync = new();

    public static void EnsureSync(AssetManager assets, string destination, CancellationToken cancel)
    {
        lock (Sync)
        {
            cancel.ThrowIfCancellationRequested();

            if (File.Exists(destination) &&
                HashFile(destination, cancel).Equals(Expected, StringComparison.OrdinalIgnoreCase))
                return;

            string staging = destination + "." + Guid.NewGuid().ToString("N") + ".partial";
            try
            {
                using (var source = assets.Open("classdata.tpk"))
                using (var target = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                {
                    byte[] buffer = new byte[65536];
                    int count;
                    while ((count = source.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        cancel.ThrowIfCancellationRequested();
                        target.Write(buffer, 0, count);
                    }
                    target.Flush(true);
                }

                string actual = HashFile(staging, cancel);
                if (!actual.Equals(Expected, StringComparison.OrdinalIgnoreCase))
                    throw new IOException($"Packaged classdata.tpk failed SHA-256 validation. Expected {Expected}, got {actual}.");

                File.Move(staging, destination, true);
            }
            finally
            {
                if (File.Exists(staging))
                    File.Delete(staging);
            }
        }
    }

    static string HashFile(string path, CancellationToken cancel)
    {
        using var algorithm = SHA256.Create();
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        byte[] buffer = new byte[65536];
        int count;
        while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancel.ThrowIfCancellationRequested();
            algorithm.TransformBlock(buffer, 0, count, buffer, 0);
        }
        algorithm.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(algorithm.Hash!);
    }
}
