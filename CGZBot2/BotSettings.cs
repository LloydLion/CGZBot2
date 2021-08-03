using LloydLion.Serialization.Common;
using System;
using System.IO;

namespace CGZBot2
{
	static class BotSettings
	{
		public readonly static string SettingRootDirectory = "settings" + Path.DirectorySeparatorChar;


		public static GuildSettings<T> Load<T>(Type handler, string key)
		{
			using var file = File.OpenRead(SettingRootDirectory + handler.FullName +
				Path.DirectorySeparatorChar + key + "." +
				SerializatorProvider.DefaultDeserializator.Format);

			var ser = SerializatorProvider.DefaultDeserializator;

			return ser.PopulateAsync(new GuildSettings<T>(), new StreamReader(file)).Result;
		}

		public static void Update<T>(Type handler, string key, Action<GuildSettings<T>> action)
		{
			var settings = Load<T>(handler, key);
			action(settings);
			Set(handler, key, settings);
		}

		public static void Set<T>(Type handler, string key, GuildSettings<T> settings)
		{
			var file = File.OpenWrite(SettingRootDirectory + handler.FullName +
				Path.DirectorySeparatorChar + key + "." +
				SerializatorProvider.DefaultDeserializator.Format);

			var ser = SerializatorProvider.DefaultSerializator;

			ser.WriteAsync(settings, new StreamWriter(file));
			file.Flush();
			file.Close();
		}
	}
}