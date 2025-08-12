using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DuplicateFinder.Core
{
    [EventSource(Name = "DuplicateFinder")]
    public class Events : EventSource
    {
        public static Events Instance { get; } = new Events();

        public void Md5Start(string fileName) => WriteEvent(1, fileName);

        public void Md5End(string fileName) => WriteEvent(2, fileName);
    }



    public static class Md5Helper
    {
        const int N = 4 * 16 * 4096;

        public static async Task<string> GetMd5Async(string file, CancellationToken token)
        {
            try
            {
                

                return await Task.Run(() => GetMd5CoreAsync(file, token), token).ConfigureAwait(false);
            }
            catch
            {
                return null;
            }
        }


        public static async Task<string> GetMd5CoreAsync(string file, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            byte[] buffer = ArrayPool<byte>.Shared.Rent(N);

            try
            {
                using (var stream = File.OpenRead(file))
                using (var md5 = System.Security.Cryptography.MD5.Create())
                {
                    Events.Instance.Md5Start(file);

                    int bytesRead;
                    do
                    {
                        token.ThrowIfCancellationRequested();

                        bytesRead = await stream.ReadAsync(buffer, 0, N).ConfigureAwait(false);
                        if (bytesRead > 0)
                        {
                            md5.TransformBlock(buffer, 0, bytesRead, null, 0);
                        }
                    } while (bytesRead > 0);

                    md5.TransformFinalBlock(buffer, 0, 0);

                    Events.Instance.Md5End(file);

                    return BitConverter.ToString(md5.Hash).Replace("-", "").ToUpperInvariant();
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }





    public class FileInfoSource
    {
        private readonly static IScheduler _scheduler = new NewThreadScheduler(n => new Thread(n) { Name = nameof(FileInfoSource) });

        public static IObservable<string[]> GetFileObservable(string path, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Observable.Create<string[]>(
                o =>
                {
                    VisitDirectoryPath(path, o, cancellationToken);

                    o.OnCompleted();
                    return Disposable.Create(() => Console.WriteLine("--Disposed--"));
                })
                .SubscribeOn(_scheduler);
        }

        private static void VisitDirectoryPath(string path, IObserver<string[]> observer, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return;

            try
            {
                observer.OnNext(Directory.GetFiles(path));

                foreach (var dir in Directory.GetDirectories(path))
                {
                    VisitDirectoryPath(dir, observer, cancellationToken);
                }
            }
            catch
            { }
        }
    }
}
