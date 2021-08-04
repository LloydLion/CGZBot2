using LloydLion.Serialization.Common;
using System;
using System.Collections.Generic;
using System.IO;

namespace CGZBot2
{
	static class HandlerState
	{
		public readonly static string StateRootDirectory = "state" + Path.DirectorySeparatorChar;


		public static GuildDictionary<T> Get<T>(Type handler, string key, Func<T> defaultValueFactory)
		{
			var path = StateRootDirectory + handler.FullName +
				Path.DirectorySeparatorChar + key + "." +
				SerializatorProvider.DefaultDeserializator.Format;

			var empty = new GuildDictionary<T>() { DefaultValueFactory = defaultValueFactory };

			if (!File.Exists(path)) return empty;

			using var file = File.OpenRead(path);

			var ser = SerializatorProvider.DefaultDeserializator;

			return ser.PopulateAsync(empty, new StreamReader(file)).Result;
		}

		public static void Set<T>(Type handler, string key, GuildDictionary<T> settings)
		{
			var file = File.OpenWrite(StateRootDirectory + handler.FullName +
				Path.DirectorySeparatorChar + key + "." +
				SerializatorProvider.DefaultDeserializator.Format);

			var ser = SerializatorProvider.DefaultSerializator;

			ser.WriteAsync(settings, new StreamWriter(file));
			file.Flush();
			file.Close();
		}
	}
}