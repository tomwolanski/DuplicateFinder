using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DuplicateFinder.Helpers
{
    public sealed class ObservableCollectionAdapter<T>: ObservableCollection<T>, IDisposable //, IObserver<T>
    {
        private IDisposable _subscription;

        public ObservableCollectionAdapter(IObservable<T> observable)
        {
            _subscription = observable
                //.Buffer(TimeSpan.FromMilliseconds(500))
                .ObserveOn(SynchronizationContext.Current)
                .Subscribe(
                    n => Add(n),
                    e => { });
        }

        public void Dispose() => _subscription.Dispose();
    }
}
