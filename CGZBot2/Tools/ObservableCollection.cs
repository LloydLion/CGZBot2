using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CGZBot2.Tools
{
	class ObservableCollection<T> : INotifyCollectionChanged, IList<T>, IList where T : INotifyPropertyChanged
	{
		private readonly System.Collections.ObjectModel.ObservableCollection<T> oc;


		public ObservableCollection()
		{
			oc = new System.Collections.ObjectModel.ObservableCollection<T>();
			oc.CollectionChanged += (s, a) => CollectionChanged?.Invoke(this, a);

			oc.CollectionChanged += (s, a) =>
			{
				if (a.NewItems != null)
					foreach (var item in a.NewItems) if (item != null) ((INotifyPropertyChanged)item).PropertyChanged += handler;
				if (a.OldItems != null)
					foreach (var item in a.OldItems) if (item != null) ((INotifyPropertyChanged)item).PropertyChanged -= handler;
			};

			void handler(object sender, PropertyChangedEventArgs args)
			{
				CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
			};
		}


		public T this[int index] { get => ((IList<T>)oc)[index]; set => ((IList<T>)oc)[index] = value; }
		object IList.this[int index] { get => ((IList)oc)[index]; set => ((IList)oc)[index] = value; }


		public int Count => ((ICollection<T>)oc).Count;

		public bool IsReadOnly => ((ICollection<T>)oc).IsReadOnly;

		public bool IsFixedSize => ((IList)oc).IsFixedSize;

		public bool IsSynchronized => ((ICollection)oc).IsSynchronized;

		public object SyncRoot => ((ICollection)oc).SyncRoot;


		public event NotifyCollectionChangedEventHandler CollectionChanged;


		public void Add(T item)
		{
			((ICollection<T>)oc).Add(item);
		}

		public int Add(object value)
		{
			return ((IList)oc).Add(value);
		}

		public void Clear()
		{
			((ICollection<T>)oc).Clear();
		}

		public bool Contains(T item)
		{
			return ((ICollection<T>)oc).Contains(item);
		}

		public bool Contains(object value)
		{
			return ((IList)oc).Contains(value);
		}

		public void CopyTo(T[] array, int arrayIndex)
		{
			((ICollection<T>)oc).CopyTo(array, arrayIndex);
		}

		public void CopyTo(Array array, int index)
		{
			((ICollection)oc).CopyTo(array, index);
		}

		public IEnumerator<T> GetEnumerator()
		{
			return ((IEnumerable<T>)oc).GetEnumerator();
		}

		public int IndexOf(T item)
		{
			return ((IList<T>)oc).IndexOf(item);
		}

		public int IndexOf(object value)
		{
			return ((IList)oc).IndexOf(value);
		}

		public void Insert(int index, T item)
		{
			((IList<T>)oc).Insert(index, item);
		}

		public void Insert(int index, object value)
		{
			((IList)oc).Insert(index, value);
		}

		public bool Remove(T item)
		{
			return ((ICollection<T>)oc).Remove(item);
		}

		public void Remove(object value)
		{
			((IList)oc).Remove(value);
		}

		public void RemoveAt(int index)
		{
			((IList<T>)oc).RemoveAt(index);
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable)oc).GetEnumerator();
		}
	}
}
