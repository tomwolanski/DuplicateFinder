using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DuplicateFinder.Helpers
{
    public static class EnumberableExtensions
    {
        public static bool IsSingleObject<T>(this IEnumerable<T> source)
        {
            return !source.Skip(1).Any();
        }

        public static bool IsMultipleObjects<T>(this IEnumerable<T> source)
        {
            return source.Skip(1).Any();
        }

        public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            foreach(var s in source)
            {
                action(s);
            }
        }

        public static IEnumerable<T> Do<T>(this IEnumerable<T> source, Action<T> action)
        {
            foreach (var s in source)
            {
                action(s);
                yield return s;
            }
        }
    }

    public static class ObservableExtensions
    {
        public static IObservable<T> ToObservable<T>(this IEnumerable<Task<T>> tasks) => ToObservable(tasks.ToArray());

        public static IObservable<T> ToObservable<T>(this Task<T>[] tasks)
        {
            return tasks.Any()
                ? Observable.Create<T>(op =>
                  {
                      var runner = new TasksObservableRunner<T>(tasks, op);
                      return Disposable.Create(() => GC.KeepAlive(runner));
                  })
                : Observable.Empty<T>();
        }

        private class TasksObservableRunner<T>
        {
            private readonly IObserver<T> _observer;
            private int _left;

            public TasksObservableRunner(Task<T>[] tasks, IObserver<T> observer)
            {
                _observer = observer;
                _left = tasks.Length;
                
                tasks.ForEach(RefisterTaskCompletion);
            }

            private void RefisterTaskCompletion(Task<T> task)
            {
                if (task.IsCompleted)
                {
                    OnTaskCompletion(task);
                }
                else
                {
                    task.ContinueWith(OnTaskCompletion);
                }
            }

            private void OnTaskCompletion(Task<T> task)
            {
                if (task.IsFaulted)
                {
                    _observer.OnError(task.Exception);
                }
                else
                {
                    _observer.OnNext(task.Result);
                }

                if (Interlocked.Decrement(ref _left) == 0)
                {
                    _observer.OnCompleted();
                }
            }

        }
    }
}
