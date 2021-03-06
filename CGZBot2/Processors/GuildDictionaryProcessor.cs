using DSharpPlus.Entities;
using LloydLion.Serialization.Common;
using LloydLion.Serialization.Common.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CGZBot2.Processors
{
	class GuildDictionaryProcessor : ITypeSerializationProcessor
	{
		public int Priority => 0;

		public bool CanProcess(Type type)
		{
			return typeof(IGuildDictionary).IsAssignableFrom(type);
		}

		public void Prepare(object origin, ObjectsDataSetBuilder builder, ISerializator invoker, string rootId = "")
		{
			var dic = (IGuildDictionary)origin;
			builder.SetNamespace(rootId);

			builder.Root.AddNumber("count", dic.Count);

			var keys = dic.Keys.OfType<DiscordGuild>().ToList();
			for (int i = 0; i < dic.Count; i++)
			{
				var val = dic[keys[i]];
				builder.Root.AddNumber("key-" + i, keys[i].Id);

				if (builder.AddObject(val, "val-" + i + "/", out var col, out var refer))
					invoker.Prepare(val, builder, "val-" + i + "/");
				builder.Root.AddReference("val-" + i, refer);
			}

			builder.ReturnNamespace();
		}

		public object Restore(object baseFeature, ObjectsDataSet obj, IDeserializator invoker, string rootId = "")
		{
			var dic = (IGuildDictionary)(baseFeature ?? new GuildDictionary<object>());
			var root = obj.GetEntry(rootId).Data;
			var count = (int)root.ReadPrimitive("count");

			for (int i = 0; i < count; i++)
			{
				var val = obj.GetEntryByRefInProp(root, "val-" + i);
				var key = (ulong)root.ReadPrimitive("key-" + i);

				object restObj = null;
				if (val != null)
				{
					var valv = val.Value;

					var processor = invoker.Processors.OrderBy(s => s.Priority).First(s => s.CanProcess(valv.Type));
					restObj = processor.Restore(null, obj, invoker, valv.Id);
				}

				dic.Add(Program.Client.GetGuildAsync(key).Result, restObj);
			}

			return dic;
		}
	}
}
