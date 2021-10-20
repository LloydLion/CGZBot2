using LloydLion.Serialization.Common;
using LloydLion.Serialization.Common.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CGZBot2.Processors.Other
{
	class ListProcessor : ITypeSerializationProcessor
	{
		public int Priority => -1;

		public bool CanProcess(Type type)
		{
			return typeof(IList).IsAssignableFrom(type) || type.GetInterfaces().Any(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IList<>));
		}

		public void Prepare(object origin, ObjectsDataSetBuilder builder, ISerializator invoker, string rootId = "")
		{
			var list = (IList)origin;
			builder.SetNamespace(rootId);

			builder.Root.AddNumber("count", list.Count);

			int i = 0;
			foreach (var el in list)
			{
				if (builder.AddObject(el, "val-" + i + "/", out var col, out var refer))
					invoker.Prepare(el, builder, "val-" + i + "/");
				builder.Root.AddReference("val-" + i, refer);
				i++;
			}

			builder.ReturnNamespace();
		}

		public object Restore(object baseFeature, ObjectsDataSet obj, IDeserializator invoker, string rootId = "")
		{
			var list = (IList)baseFeature;
			var rootEntry = obj.GetEntry(rootId);
			var root = rootEntry.Data;

			if(list == null)
			{
				list = (IList)rootEntry.Type.GetConstructor(Array.Empty<Type>()).Invoke(null);
			}

			var count = (int)root.ReadPrimitive("count");

			for (int i = 0; i < count; i++)
			{
				var val = obj.GetEntryByRefInProp(root, "val-" + i);

				object restObj = null;
				if (val != null)
				{
					var valv = val.Value;

					var processor = invoker.Processors.OrderBy(s => s.Priority).First(s => s.CanProcess(valv.Type));
					restObj = processor.Restore(null, obj, invoker, valv.Id);
				}

				list.Add(restObj);
			}

			return list;
		}
	}
}
