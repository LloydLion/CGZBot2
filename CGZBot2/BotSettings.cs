using LloydLion.Serialization.Common;
using System;
using System.IO;

namespace CGZBot2
{
	static class BotSettings
	{
		public readonly static string SettingRootDirectory = "settings" + Path.DirectorySeparatorChar;


		public static GuildDictionary<T> Load<T>(Type handler, string key)
		{
			using var file = File.OpenRead(SettingRootDirectory + handler.FullName +
				Path.DirectorySeparatorChar + key + "." +
				SerializatorProvider.DefaultDeserializator.Format);

			var ser = SerializatorProvider.DefaultDeserializator;

			return ser.PopulateAsync(new GuildDictionary<T>(), new StreamReader(file)).Result;
		}

		public static void Update<T>(Type handler, string key, Action<GuildDictionary<T>> action)
		{
			var settings = Load<T>(handler, key);
			action(settings);
			Set(handler, key, settings);
		}

		public static void Set<T>(Type handler, string key, GuildDictionary<T> settings)
		{
			var file = File.OpenWrite(SettingRootDirectory + handler.FullName +
				Path.DirectorySeparatorChar + key + "." +
				SerializatorProvider.DefaultDeserializator.Format);

			var ser = SerializatorProvider.DefaultSerializator;

			ser.WriteAsync(settings, new StreamWriter(file)).Wait();
			file.Flush();
			file.Close();
		}
	}
}