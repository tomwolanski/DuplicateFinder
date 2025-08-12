using DuplicateFinder.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using DuplicateFinder.Helpers;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows.Forms;

namespace DuplicateFinder.Scanner
{

    public class ScannerVM : VmBase
    { 
        private readonly Database _db = Database.Instance;

        private CancellationTokenSource _cts;
        private bool _isScanRunning;

        public Command StartScan { get; }
        public Command StopScan { get; }

        public Command AddScanDirectory { get; }
        public Command RemoveScanDirectory { get; }

        public int FilesDiscoveredCount { get; set; }
        public int FilesCalculatedCount { get; set; }

        public string LastFileCalculated { get; set; }

        public bool IsScanRunninng 
        { 
            get => _isScanRunning; 
            private set
            {
                _isScanRunning = value;
                StartScan.RaiseCanExecute();
                StopScan.RaiseCanExecute();
            }
        }
        
        public bool IsScanStopped => !IsScanRunninng;

        public ObservableCollection<string> DirectoriesToScan { get; } = new ObservableCollection<string>();

        public ScannerVM()
        {
            StartScan = new Command(StartScanImplAsync, _ => IsScanStopped);
            StopScan = new Command(StopScanImpl, _ => IsScanRunninng);

            AddScanDirectory = new Command(AddScanDirectoryImpl);
            RemoveScanDirectory = new Command(RemoveScanDirectoryImpl);   
        }

        public override void OnLoad()
        {
            _db.SelectedDirectories
                .ReadAll()
                .ForEach(DirectoriesToScan.Add);
        }

        public override void OnClose()
        {
            base.OnClose();
            StopScanImpl(null);
        }

        private void AddScanDirectoryImpl(object obj)
        {
            var dialog = new FolderBrowserDialog();
            var result = dialog.ShowDialog();

            if (result == DialogResult.OK)
            {
                DirectoriesToScan.Add(dialog.SelectedPath);
                _db.SelectedDirectories.Add(dialog.SelectedPath);
            }
        }

        private void RemoveScanDirectoryImpl(object obj)
        {
            var s = obj as string;
            
            if (s != null)
            {
                DirectoriesToScan.Remove(s);
                _db.SelectedDirectories.Remove(s);
            }
        }
        
        private async void StartScanImplAsync(object obj)
        {
            IsScanRunninng = true;

            _cts = new CancellationTokenSource();

            try
            {
                await _db.Files.ClearAsync(_cts.Token);
                await RunFlowAsync(DirectoriesToScan, _cts.Token);
            }
            catch (TaskCanceledException)
            { }

            IsScanRunninng = false;

        //    RequestClose();
        }

        private void StopScanImpl(object obj)
        {
            _cts?.Cancel();
        }

        private async Task RunFlowAsync(IEnumerable<string> paths, CancellationToken token)
        {
            async Task<FileEntry> CalcMd5(string file)
            {
                var md5 = await Md5Helper.GetMd5Async(file, token).ConfigureAwait(false);
                return FileEntry.FromFile(file, md5);
            }
    
            var fileObservable = paths.Select(path => FileInfoSource.GetFileObservable(path, token))
                .Merge()
                .Publish();

            var md5Observable = fileObservable
                .TakeWhile(_ => !token.IsCancellationRequested)
                //.ObserveOn(TaskPoolScheduler.Default)
               // .ObserveOn(System.Reactive.Concurrency.Scheduler.Default)
                .SelectMany(files => files.Select(CalcMd5).ToObservable())
                .Publish();

            var fileCountSubscrption = fileObservable
                .Scan(0, (acc, curr) => acc + curr.Length)
                .Sample(TimeSpan.FromMilliseconds(20))
                .Subscribe(n => FilesDiscoveredCount = n);

            var md5CountSubscrption = md5Observable
                .Scan(0, (acc, curr) => acc + 1)
                .Sample(TimeSpan.FromMilliseconds(20))
                .TakeWhile(_ => !token.IsCancellationRequested)
                .Subscribe(n => FilesCalculatedCount = n);

            var lastMd5Subscription = md5Observable
                .Sample(TimeSpan.FromMilliseconds(20))
                .TakeWhile(_ => !token.IsCancellationRequested)
                .Subscribe(n => LastFileCalculated = n.Path);

            var sinkSubscription = md5Observable
                .ObserveOn(Database._scheduler)
                .TakeWhile(_ => !token.IsCancellationRequested)
                .Subscribe(n => _db.Files.Add(n));

            md5Observable.Connect();
            fileObservable.Connect();

            await Task.WhenAll(
                fileObservable.ToTask(token), 
                md5Observable.ToTask(token));

            fileCountSubscrption.Dispose();
            md5CountSubscrption.Dispose();
            lastMd5Subscription.Dispose();
            sinkSubscription.Dispose();
        }
    }

    class MyChed : IScheduler
    {
        public DateTimeOffset Now => throw new NotImplementedException();

        public IDisposable Schedule<TState>(TState state, Func<IScheduler, TState, IDisposable> action)
        {
            throw new NotImplementedException();
        }

        public IDisposable Schedule<TState>(TState state, TimeSpan dueTime, Func<IScheduler, TState, IDisposable> action)
        {
            throw new NotImplementedException();
        }

        public IDisposable Schedule<TState>(TState state, DateTimeOffset dueTime, Func<IScheduler, TState, IDisposable> action)
        {
            throw new NotImplementedException();
        }
    }
}
