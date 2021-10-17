using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
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

		public static void TryDelete(this DiscordChannel channel)
		{
			try { channel.DeleteAsync().Wait(); } catch (Exception) { }
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

		public static void AddRange<T>(this ICollection<T> col, IEnumerable<T> toAdd)
		{
			foreach (var item in toAdd)
			{
				col.Add(item);
			}
		}

		public static DisRole ToDisRole(this DiscordRole role, DiscordGuild guild)
		{
			return new DisRole(role, guild);
		}

		public static DateTime AddTo(this TimeSpan time, DateTime dateTime) => dateTime + time;

		public static DiscordMessage SendDicertMessage(this DiscordMember user, string content)
		{
			try
			{
				return user.CreateDmChannelAsync().Result.SendMessageAsync(s => s.WithContent(content)).Result;
			}
			catch (Exception)
			{
				return null;
			}
		}

		public static bool IsExist(this DiscordMessage msg)
		{
			try
			{
				msg.GetReactionsAsync(DiscordEmoji.FromName(Program.Client, ":ok_hand:")).Wait();
				return true;
			}
			catch(AggregateException ex)
			{
				if (ex.InnerException is NotFoundException) return false;
				else throw;
			}
		}

		public static bool IsExist(this DiscordChannel channel)
		{
			try
			{
				channel.ModifyAsync(s => { }).Wait();
				return true;
			}
			catch(AggregateException ex)
			{
				if (ex.InnerException is NotFoundException) return false;
				else throw;
			}
		}

		public static Task<T> StartAndWait<T>(this Task<T> obj)
		{
			obj.Start();
			obj.Wait();
			return obj;
		}
		
		public static Task StartAndWait(this Task obj)
		{
			obj.Start();
			obj.Wait();
			return obj;
		}
	}
}
