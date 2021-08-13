using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CGZBot2
{
	static class Extensions
	{
		public static void TryDelete(this DiscordMessage msg)
		{
			try { msg.DeleteAsync().Wait(); } catch (Exception) { }
		}

		public static void TryDeleteAfter(this DiscordMessage msg, int timeout)
		{
			Task.Run(() =>
			{
				Thread.Sleep(timeout);
				msg.TryDelete();
			});
		}

		public static void TryDeleteAfter(this Task<DiscordMessage> tmsg, int timeout)
		{
			Task.Run(() =>
			{
				Thread.Sleep(timeout);
				tmsg.Result.TryDelete();
			});
		}

		public static void CopyTo<T>(this IReadOnlyCollection<T> obj, ICollection<T> target)
		{
			foreach (var el in obj)
				target.Add(el);
		}

		public static void CopyTo<T>(this IReadOnlyList<T> obj, IList<T> target)
		{
			foreach (var el in obj)
				target.Add(el);
		}
	}
}
