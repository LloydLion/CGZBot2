using LloydLion.Serialization.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace CGZBot2
{
	static class HandlerState
	{
		public readonly static string StateRootDirectory = "state" + Path.DirectorySeparatorChar;


		private readonly static Dictionary<(Type, string), IGuildDictionary> states = new();


		public static GuildDictionary<T> Get<T>(Type handler, string key, Func<T> defaultValueFactory)
		{
			var path = StateRootDirectory + handler.FullName +
				Path.DirectorySeparatorChar + key + "." +
				SerializatorProvider.DefaultDeserializator.Format;

			var empty = new GuildDictionary<T>() { DefaultValueFactory = defaultValueFactory };

			states.Add((handler, key), empty);

			if (!File.Exists(path)) return empty;

			using var file = File.OpenRead(path);

			var ser = SerializatorProvider.DefaultDeserializator;

			return ser.PopulateAsync(empty, new StreamReader(file)).Result;
		}

		public static void Set(Type handler, string key, IGuildDictionary settings)
		{
			var path = StateRootDirectory + handler.FullName +
				Path.DirectorySeparatorChar + key + "." +
				SerializatorProvider.DefaultDeserializator.Format;

			if (File.Exists(path)) File.Delete(path);

			var file = File.OpenWrite(path);

			var ser = SerializatorProvider.DefaultSerializator;

			ser.WriteAsync(settings, new StreamWriter(file)).Wait();
			file.Flush();
			file.Close();
		}

		public static void SaveAll()
		{
			foreach (var item in states)
				Set(item.Key.Item1, item.Key.Item2, item.Value);
		}
	}
}