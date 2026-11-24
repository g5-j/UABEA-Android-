using Android.Content.Res;
using Uabea.Core;

namespace Uabea.Android;

static class ClassDatabaseInstaller
{
    const string Expected = "dcea167eb5e8d17e7aa33c933a7332e10f88642f7e6e791b0cc2a6b7f44ec247";

    // Multiple Android processes can enter Activity.OnCreate at the same time.
    // Serialize installation across processes, not just across threads.
    public static void EnsureSync(AssetManager assets, string destination, CancellationToken cancel)
    {
        string lockPath = destination + ".install.lock";
        FileStream? installationLock = null;
        DateTime deadline = DateTime.UtcNow.AddSeconds(20);

        while (installationLock is null)
        {
            cancel.ThrowIfCancellationRequested();
            try
            {
                installationLock = new FileStream(
                    lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(50);
            }
        }

        using (installationLock)
        {
            cancel.ThrowIfCancellationRequested();
            if (File.Exists(destination) &&
                StreamTools.HashFile(destination, cancel).Equals(Expected, StringComparison.OrdinalIgnoreCase))
                return;

            // A unique staging path also protects against stale files left by a killed process.
            string staging = destination + "." + Guid.NewGuid().ToString("N") + ".partial";
            try
            {
                using (var source = assets.Open("classdata.tpk"))
                using (var target = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None))
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

                if (!StreamTools.HashFile(staging, cancel).Equals(Expected, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("La base de clases empaquetada está incompleta o dañada.");

                File.Move(staging, destination, true);
            }
            finally
            {
                if (File.Exists(staging))
                    File.Delete(staging);
            }
        }
    }
}
