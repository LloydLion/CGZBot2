using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Emzi0767.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CGZBot2.Tools
{
	class Utils
	{
		public static Task<ComponentInteractionCreateEventArgs> WaitForButton(Func<DiscordMessage> msgGetter, string id, CancellationToken token = default)
		{
			ComponentInteractionCreateEventArgs args = null;
			return new Task<ComponentInteractionCreateEventArgs>(() => { Program.Client.ComponentInteractionCreated += lambda; while (args == null && !token.IsCancellationRequested)
				Thread.Sleep(100); Program.Client.ComponentInteractionCreated -= lambda; return args; });


			Task lambda(BaseDiscordClient s, ComponentInteractionCreateEventArgs a) { if (a.Message == msgGetter() && a.Id == id) args = a; return Task.CompletedTask; }
		}

		public static Task<ComponentInteractionCreateEventArgs> WaitForButton(DiscordMessage msg, string id, CancellationToken token = default) => WaitForButton(() => msg, id, token);


		public static Task<MessageCreateEventArgs> WaitForMessage(Func<DiscordUser> usrGetter, Func<DiscordChannel> chGetter, CancellationToken token = default)
		{
			MessageCreateEventArgs args = null;
			return new Task<MessageCreateEventArgs>(() => { Program.Client.MessageCreated += lambda; while (args == null && !token.IsCancellationRequested) Thread.Sleep(100); Program.Client.MessageCreated -= lambda; return args; }, token);


			Task lambda(BaseDiscordClient s, MessageCreateEventArgs a) { if (a.Author == usrGetter() && a.Channel == chGetter()) args = a; return Task.CompletedTask; }
		}

		public static Task<MessageCreateEventArgs> WaitForMessage(DiscordUser user, DiscordChannel channel, CancellationToken token = default) => WaitForMessage(() => user, () => channel, token);


		public static Task WaitFor(Func<bool> predicate)
		{
			return new Task(() => { while (predicate() == false) Thread.Sleep(1000); });
		}

		public static Task WaitForAny(params Func<bool>[] predicates)
		{
			return new Task(() => { while (predicates.Any(s => s.Invoke()) == false) Thread.Sleep(1000); });
		}

		public static Task WaitForAll(params Func<bool>[] predicates)
		{
			return new Task(() => { while (predicates.All(s => s.Invoke()) == false) Thread.Sleep(1000); });
		}
	}
}
