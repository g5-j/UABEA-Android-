using Android.Content.Res;
using System.Security.Cryptography;
using Uabea.Core;

namespace Uabea.Android;

static class ClassDatabaseInstaller
{
    const string Expected = "DCEA167EB5E8D17E7AA33C933A7332E10F88642F7E6E791B0CC2A6B7F44EC247";

    public static void EnsureSync(AssetManager assets, string destination, CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();

        if (File.Exists(destination) &&
            StreamTools.HashFile(destination, cancel).Equals(Expected, StringComparison.OrdinalIgnoreCase))
            return;

        // Hash the bytes as they are copied. Reopening a FileShare.None staging file
        // through File.OpenRead is not reliable on Android's Unix file implementation.
        string staging = destination + "." + Guid.NewGuid().ToString("N") + ".partial";
        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            using (var source = assets.Open("classdata.tpk"))
            using (var target = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            {
                byte[] buffer = new byte[65536];
                int count;
                while ((count = source.Read(buffer, 0, buffer.Length)) > 0)
                {
                    cancel.ThrowIfCancellationRequested();
                    target.Write(buffer, 0, count);
                    hash.AppendData(buffer, 0, count);
                }
                target.Flush(true);
            }

            string actual = Convert.ToHexString(hash.GetHashAndReset());
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
