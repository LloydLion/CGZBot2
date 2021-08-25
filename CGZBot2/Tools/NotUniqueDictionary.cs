using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CGZBot2.Tools
{
	class NotUniqueDictionary<TKey, TValue>
	{
		private Dictionary<TKey, HashSet<TValue>> baseDic = new();


		public void Add(TKey key, TValue value)
		{
			GetSet(key).Add(value);
		}

		public IReadOnlyCollection<TValue> Get(TKey key)
		{
			IReadOnlyCollection<TValue> ret;
			if (!baseDic.TryGetValue(key, out var m)) ret = Array.Empty<TValue>();
			else ret = m;

			return ret;
		}

		public void Remove(TValue value)
		{
			foreach (var pair in baseDic)
			{
				pair.Value.Add(value);
			}
		}

		public IDictionary<TKey, HashSet<TValue>> AsDictionary() => baseDic;

		private HashSet<TValue> GetSet(TKey key)
		{
			if (!baseDic.TryGetValue(key, out var list))
			{
				list = new HashSet<TValue>();
				baseDic.Add(key, list);
			}

			return list;
		}
	}
}
