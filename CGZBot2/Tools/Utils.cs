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
		public static Task<ComponentInteractionCreateEventArgs> WaitForButton(Func<DiscordMessage> msgGetter, string id)
		{
			ComponentInteractionCreateEventArgs args = null;
			AsyncEventHandler<BaseDiscordClient, ComponentInteractionCreateEventArgs> lambda = (s, a) => { if (a.Message == msgGetter() && a.Id == id) args = a; return Task.CompletedTask; };
			Program.Client.ComponentInteractionCreated += lambda;
			return Task.Run(() => { while (args == null) Thread.Sleep(100); Program.Client.ComponentInteractionCreated -= lambda; return args; });
		}

		public static Task<ComponentInteractionCreateEventArgs> WaitForButton(DiscordMessage msg, string id) => WaitForButton(() => msg, id);
	}
}
