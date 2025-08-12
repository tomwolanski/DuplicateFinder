using DuplicateFinder.Helpers;
using LiteDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DuplicateFinder.Core
{
    public class Database
    {
        private readonly static Lazy<Database> _instance = new Lazy<Database>(() => new Database());
        public static Database Instance => _instance.Value;

        internal readonly static IScheduler _scheduler = new NewThreadScheduler(n => new Thread(n) { Name = nameof(Database) });

        public FileImpl Files { get; }
        public SelectedDirectoriesImpl SelectedDirectories { get; }

        private readonly LiteDatabase _db;
        
        private Database()
        {
            _db = new LiteDatabase(@"MyData.db");

            Files = new FileImpl(_db.GetCollection<FileEntry>());
            SelectedDirectories = new SelectedDirectoriesImpl(_db.GetCollection<SelectedDirectoryEntry>());
        }

        public void Dispose()
        {
            _db.Dispose();
        }

        public class FileImpl
        {
            private readonly LiteCollection<FileEntry> _fileEntryCollection;

            public FileImpl(LiteCollection<FileEntry> fileEntryCollection)
            {
                _fileEntryCollection = fileEntryCollection;
            }

            public Task ClearAsync(CancellationToken token) => Task.Run(() => _fileEntryCollection.Delete(Query.All()), token);

            public void Add(FileEntry file) => _fileEntryCollection.Insert(file);

            public IEnumerable<FileEntry> ReadAll() => _fileEntryCollection.FindAll(); 
        }

        public class SelectedDirectoriesImpl
        {
            private readonly LiteCollection<SelectedDirectoryEntry> _selectedDirectoriesCollection;

            public SelectedDirectoriesImpl(LiteCollection<SelectedDirectoryEntry> selectedDirectoriesCollection)
            {
                _selectedDirectoriesCollection = selectedDirectoriesCollection;
            }

            public void Add(string directory) => _selectedDirectoriesCollection.Insert(new SelectedDirectoryEntry { Value = directory });

            public void Remove(string directory) => _selectedDirectoriesCollection.Delete(n => n.Value == directory);

            public IEnumerable<string> ReadAll() => _selectedDirectoriesCollection.FindAll().Select(n => n.Value);
        }

        
    }

    public class SelectedDirectoryEntry
    {
        public Guid Id { get;set; }
        public string Value { get; set; }
    }

    public class FileEntry
    {
        public Guid Id { get; set; }
        public string Path { get; set; }
        public string Extension { get; set; }
        public string Name { get; set; }
        public string Md5 { get; set; }

        public static FileEntry FromFile(string file, string md5)
        {
            return new FileEntry
            {
                Path = file,
                Name = System.IO.Path.GetFileName(file),
                Extension = System.IO.Path.GetExtension(file),
                Md5 = md5
            };
        }
    }
}
