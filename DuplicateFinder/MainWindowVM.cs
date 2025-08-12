using DuplicateFinder.Core;
using DuplicateFinder.Helpers;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;

namespace DuplicateFinder
{
    public delegate IEnumerable<FileEntry> DuplicateSelectionHandler(FileEntry file);



    public class ObservableCollectionExtended<T> : ObservableCollection<T>
    {
        public void AddRange(IEnumerable<T> items)
        {
            CheckReentrancy();

            foreach (var i in items)
            {
                base.Items.Add(i);
            }

            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }




    public class MainWindowVM : VmBase
    {
        private readonly Database _db = Database.Instance;

        private int _duplicateSelectorType = 0;

        private bool _showOnlyDuplicated;

        private BehaviorSubject<int> _duplicateSelectionHandlerObs = new BehaviorSubject<int>(0);
        private BehaviorSubject<bool> _showDuplicateOnlyObs = new BehaviorSubject<bool>(false);

        public ObservableCollectionExtended<FileCopyVm> Duplicates { get; } = new ObservableCollectionExtended<FileCopyVm>();

        public ObservableCollectionExtended<FileStructureItemBase> FileStructure { get; } = new ObservableCollectionExtended<FileStructureItemBase>();

        public int DuplicateSelectionType 
        { 
            get => _duplicateSelectorType; 
            set
            {
                _duplicateSelectorType = value;
                _duplicateSelectionHandlerObs.OnNext(value);
                _showDuplicateOnlyObs.OnNext(ShowOnlyDuplicated);
                Duplicates.Clear();
            }
        }

        public bool ShowOnlyDuplicated
        {
            get => _showOnlyDuplicated;
            set
            {
                _showOnlyDuplicated = value;
                _showDuplicateOnlyObs.OnNext(value);
            }
        }

        public bool IsLoading { get; private set; }

        public ICommand NewScan { get; }

        public ICommand FileSelected { get; }
        
        public MainWindowVM()
        {
            NewScan = new Command(_ =>
            {
                new Scanner.ScannerWindow().ShowDialog();
                Reload();
            });

            FileSelected = new Command(FileSelectedImpl);
        }


        public override void OnLoad()
        {
            base.OnLoad();

            Reload();
        }

        public void Reload()
        {
            IsLoading = true;

            FileStructure.Clear();

            var data = GetFileStructoreAsync();
            //data.ForEach(FileStructure.Add);

            FileStructure.AddRange(data);

            IsLoading = false;
        }

        private IEnumerable<FileStructureItemBase> GetFileStructoreAsync()
        {
            var files = _db.Files.ReadAll().ToArray();
            var duplicateSelector = GetDuplicateSelector(files, _duplicateSelectionHandlerObs.Value);

            return GetFromFiles(files);
        }


        private FileStructureItemBase[] GetFromFiles(IEnumerable<FileEntry> files)
        {
            var settingsObs = Observable.CombineLatest(
                _duplicateSelectionHandlerObs.Select(type => GetDuplicateSelector(files, type)),
                _showDuplicateOnlyObs,
                Tuple.Create)
                .Publish();

            var fastSett = new FastSubject<Tuple<DuplicateSelectionHandler, bool>>();
            settingsObs.Subscribe(fastSett);

            string[] GetDirParts(string file)
            {
                var separator = new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
                return file.Split(separator);
            }

            FileStructureItemBase[] GetNodes(IEnumerable<Tuple<FileEntry, string[]>> pairs, int groupLevel)
            {
                return pairs
                    //.AsParallel()
                    .GroupBy(n => n.Item2[groupLevel])
                    .Select(n => GetNode(groupLevel, n))
                    .ToArray();
            }

            FileStructureItemBase GetNode(int groupLevel, IGrouping<string, Tuple<FileEntry, string[]>> group)
            {
                if (group.IsSingleObject())
                {
                    var file = group.Single().Item1;

                    return new FileItemVm(file, fastSett);
                }

                var pathParts = group.First().Item2.Select(n => n.Replace(":", ":\\")).Take(groupLevel + 1).ToArray();
                var fullPath = Path.Combine(pathParts);

                return new DirectoryItemVm(group.Key, fullPath, GetNodes(group, groupLevel + 1));
            }

            var filePartsPair = files
                .AsParallel()
                .Select(n =>  Tuple.Create(n, GetDirParts(n.Path)))
                .ToArray();

            var nodes =  GetNodes(filePartsPair, 0);

            settingsObs.Connect();

            return nodes;
        }
    
        private DuplicateSelectionHandler GetDuplicateSelector(IEnumerable<FileEntry> files, int type)
        {
            switch(type)
            {
                case 0: // file name
                    {
                        var lookup = files.ToLookup(n => n.Name);
                        return f => lookup[f.Name];
                    }

                case 1: // md5
                    {
                        var lookup = files.ToLookup(n => n.Md5);
                        return f => lookup[f.Md5];
                    }

                case 2: // name + md5
                    {
                        var lookup = files.ToLookup(n => Tuple.Create(n.Name, n.Md5));
                        return f => lookup[Tuple.Create(f.Name, f.Md5)];
                    }
            };

            throw new NotSupportedException();
        }


        private void FileSelectedImpl(object obj)
        {
            var file = obj as FileItemVm;

            if (file != null)
            {
             //   Duplicates.ForEach(n => n.Dispose());
                Duplicates.Clear();

                var vms = file.Duplicates.Select(n => new FileCopyVm(n));
                Duplicates.AddRange(vms);

                //file.Duplicates
                //    .Select(n => new FileCopyVm(n))
                //    .ForEach(n => Duplicates.Add(n));

                var currentSelected = Duplicates.Single(n => n.Path == file.File.Path);
                currentSelected.IsHiglighted = true;
            }
        }
    }




    public class FileCopyVm : VmBase
    {
        public bool IsHiglighted { get; set; }
        public bool FileExists { get; private set; } = true;
        public DateTime FileLastAccessDate { get; private set; }

        public string Path { get; }
        public string Md5 { get; }

        public ICommand Open { get; }
        public ICommand Delete { get; }

        public ImageSource Icon => FileIconHelper.FindIconForFilename(Path, true);

        public FileCopyVm(FileEntry file)
        {
            Path = file.Path;
            Md5 = file.Md5;

            Task.Run(() => RefreshFileDetailsAsync());

            Open = new Command(_ => Process.Start(Path));

            Delete = new Command(DeleteFileImpl);
        }

        private void DeleteFileImpl(object obj)
        {
            FileSystem.DeleteFile(Path, UIOption.AllDialogs, RecycleOption.SendToRecycleBin, UICancelOption.DoNothing);

            RefreshFileDetailsAsync();
        }

        private void RefreshFileDetailsAsync()
        {
            FileExists = System.IO.File.Exists(Path);
            if (FileExists)
            {
                FileLastAccessDate = System.IO.File.GetLastWriteTime(Path);
            }
        }
    }

    public class FileItemVm : FileStructureItemBase
    {
        private readonly IDisposable _subscription;
        
        public FileEntry File { get; }

        public IEnumerable<FileEntry> Duplicates { get; private set; }
        public int DuplicateCount { get; private set; }
        public bool FileExists { get; private set; } = true;

        public override string Name => File.Name;
        public override string FullPath => File.Path;

        public ImageSource Icon => FileIconHelper.FindIconForFilename(Name, true);

        public FileItemVm(FileEntry file,  IObservable<Tuple<DuplicateSelectionHandler, bool>> settingsObs)
        {
            File = file;

            Task.Run(() => RefreshFileDetailsAsync());

            _subscription = settingsObs
                .Subscribe(settings =>
                    {
                        Duplicates = settings.Item1.Invoke(File);
                        DuplicateCount = Duplicates.Count();
                        HasDuplicates = DuplicateCount > 1;
                        IsVisible = !settings.Item2 || HasDuplicates;
                    });
        }

        public override void Dispose()
        {
            _subscription.Dispose();
        }

        private void RefreshFileDetailsAsync()
        {
            FileExists = System.IO.File.Exists(File.Path);
        }
    }

    public class DirectoryItemVm : FileStructureItemBase
    {
        public string IsExpanded { get; set; }
        public override string Name { get; }
        public override string FullPath { get; }
        public FileStructureItemBase[] Children { get; }

        public DirectoryItemVm(string name, string fullPath, FileStructureItemBase[] children)
        {
            Name = name;
            FullPath = fullPath;
            Children = children;
            Children.ForEach(n => 
            { 
                n.HasDuplicatesChanged += OnChildDuplicateChange; 
                n.VisiblityChanged += OnChildVisibilityChange;
            });
        }

        public override void Dispose()
        {
            Children.ForEach(n => n.Dispose());
        }

        private void OnChildDuplicateChange()
        {
            HasDuplicates = Children.Any(n => n.HasDuplicates);
        }

        private void OnChildVisibilityChange()
        {
            IsVisible = Children.Any(n => n.IsVisible);
        }
    }

    public abstract class FileStructureItemBase : VmBase, IDisposable
    {
        private bool _hasDuplicates;
        private bool _isVisible = true;

        public abstract string Name { get; }
        public abstract string FullPath { get; }

        public ICommand OpenNode { get; }

        public event Action HasDuplicatesChanged;
        public event Action VisiblityChanged;

        public bool HasDuplicates 
        { 
            get => _hasDuplicates; 
            protected set
            {
                _hasDuplicates = value;
                HasDuplicatesChanged?.Invoke();
            }
        }

        public bool IsVisible
        {
            get => _isVisible;
            protected set
            {
                _isVisible = value;
                VisiblityChanged?.Invoke();
            }
        }

        protected FileStructureItemBase()
        {
            OpenNode = new Command(OpenNodeImpl);
        }

        public virtual void Dispose()
        { }

        protected virtual void OpenNodeImpl(object obj)
        {
            if (File.Exists(FullPath) || Directory.Exists(FullPath))
            {
                var args = $"/select,\"{FullPath}\"";

                Process.Start("explorer.exe", args);
            }
        }
    }








}
