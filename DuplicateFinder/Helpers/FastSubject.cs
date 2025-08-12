using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

namespace DuplicateFinder.Helpers
{
    sealed class FastSubject<T> : ISubject<T>
    {
        private event Action onCompleted;
        private event Action<Exception> onError;
        private event Action<T> onNext;

        public FastSubject()
        {
            onCompleted += () => { };
            onError += error => { };
            onNext += value => { };
        }

        public void OnCompleted()
        {
            this.onCompleted();
        }

        public void OnError(Exception error)
        {
            this.onError(error);
        }

        public void OnNext(T value)
        {
            this.onNext(value);
        }

        public IDisposable Subscribe(IObserver<T> observer)
        {
            this.onCompleted += observer.OnCompleted;
            this.onError += observer.OnError;
            this.onNext += observer.OnNext;

            return Disposable.Create(() =>
            {
                this.onCompleted -= observer.OnCompleted;
                this.onError -= observer.OnError;
                this.onNext -= observer.OnNext;
            });
        }
    }
}
