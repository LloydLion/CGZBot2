using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CGZBot2.Tools
{
	static class DialogUtils
	{
		public static Action<DialogContext, DialogMessage.ShowContext> ShowText(string text, string key)
		{
			return (dc, sc) =>
			{
				var msg = dc.Channel.SendMessageAsync(text).Result;
				sc.DynamicParameters.Add(key, msg);
			};
		}

		public static Action<DialogContext, DialogMessage.ShowContext> DeleteMessage(string key)
		{
			return (dc, sc) =>
			{
				((DiscordMessage)sc.DynamicParameters[key]).TryDelete();
			};
		}

		public static Action<DialogContext, DialogMessage.ShowContext> ShowButtonList<T>(Func<DialogContext, IReadOnlyCollection<T>> objectsGetter, Func<DialogContext, T, string> selector, Func<DialogContext, T, bool> filter, string text, string msgKey, string colKey)
		{
			return (dc, sc) =>
			{
				var pairs = new List<ShowedButtonsList.ButtonObjectPair>();

				var msg = dc.Channel.SendMessageAsync(s =>
				{
					s.WithContent(text);

					int idc = 0;
					var col = objectsGetter(dc);

					foreach (var obj in col)
					{
						if (filter(dc, obj) == false) continue;

						s.AddComponents(new DiscordButtonComponent(ButtonStyle.Primary, idc.ToString(), selector(dc, obj), false, null));
						pairs.Add(new ShowedButtonsList.ButtonObjectPair(obj, idc.ToString()));
						idc++;
					}
				}).Result;

				sc.DynamicParameters.Add(msgKey, msg);
				dc.DynamicParameters.Add(colKey, new ShowedButtonsList(msg, pairs));
			};
		}

		public static Func<DialogContext, ITransitWorker<MessageUID>> ButtonSelectorTransitFactory(string colKey)
		{
			return (dctx) => new TaskTransitWorker<MessageUID>(token =>
			{
				return new Task(() =>
				{
					var buts = (ShowedButtonsList)dctx.DynamicParameters[colKey];

					var tasks = buts.Buttons.Select(s => { var task = Utils.WaitForButton(buts.Message, s.ButtonId, token); task.Start(); return task; }).ToArray();

				retry:

					int index = -1;
					try { index = Task.WaitAny(tasks, token); }
					catch (OperationCanceledException) { }

					if (index == -1 || token.IsCancellationRequested) return;
					var args = tasks[index].Result;

					var builder = new DiscordInteractionResponseBuilder().AsEphemeral(true);

					var member = dctx.Channel.Guild.GetMemberAsync(args.User.Id).Result;
					if (member == dctx.Caller)
					{
						try { args.Interaction.CreateResponseAsync(InteractionResponseType.Pong).Wait(); }
						catch (Exception ex)
						{
							Program.Client.Logger.Log(LogLevel.Warning, ex, "Exception while sending interaction responce");
						}

						dctx.DynamicParameters[colKey] = buts.Buttons.Single(s => s.ButtonId == args.Id).Object;
					}
					else
					{
						builder.WithContent("Это не ваш диалог");
						try { args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, builder).Wait(); }
						catch (Exception ex)
						{
							Program.Client.Logger.Log(LogLevel.Warning, ex, "Exception while sending interaction responce");
						}

						tasks[index] = Utils.WaitForButton(buts.Message, args.Id, token);
						tasks[index].Start();

						goto retry;
					}
				}, token);
			});
		}

		public static Func<DialogContext, ITransitWorker<MessageUID>> WaitForButtonTransitFactory(Func<DialogContext, DiscordMessage> msg, Predicate<DialogContext> action, string bntId)
		{
			return (dctx) => new TaskTransitWorker<MessageUID>((token) =>
			{
				return new Task(() =>
				{
				retry:
					var args = Utils.WaitForButton(() => msg(dctx), bntId).StartAndWait().Result;
					if (token.IsCancellationRequested) return;
					if (args.User != dctx.Caller)
					{
						DiscordInteractionResponseBuilder builder = new DiscordInteractionResponseBuilder().WithContent("Это не ваш диалог").AsEphemeral(true);

						try { args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, builder).Wait(); }
						catch (Exception ex)
						{
							Program.Client.Logger.Log(LogLevel.Warning, ex, "Exception while sending interaction responce");
						}
						goto retry;
					}
					else
					{
						try { args.Interaction.CreateResponseAsync(InteractionResponseType.Pong).Wait(); }
						catch (Exception ex)
						{
							Program.Client.Logger.Log(LogLevel.Warning, ex, "Exception while sending interaction responce");
						}

						if (!action(dctx)) goto retry;
					}
				});
			});
		}

		public static Func<DialogContext, ITransitWorker<MessageUID>> WaitForMessageTransitFactory(Func<DiscordMessage, DialogContext, bool> actionPredicate)
		{
			return (dctx) => new TaskTransitWorker<MessageUID>((token) =>
			{
				return new Task(() =>
				{
				retry:
					var task = Utils.WaitForMessage(dctx.Caller, dctx.Channel, token).StartAndWait();
					if (token.IsCancellationRequested) return;
					if (!actionPredicate(task.Result.Message, dctx)) goto retry;
				}, token);
			});
		}

		public static Func<DialogContext, ITransitWorker<MessageUID>> TimeoutTransitFactory(int timeout = 50000)
		{
			return (dctx) => new TaskTransitWorker<MessageUID>((token) => new Task(() => { Thread.Sleep(timeout); if (!token.IsCancellationRequested) dctx.DynamicParameters.Add("bad", new object()); }));
		}


		public struct ShowedButtonsList
		{
			public ShowedButtonsList(DiscordMessage message, IReadOnlyCollection<ButtonObjectPair> buttons)
			{
				Message = message;
				Buttons = buttons;
			}


			public DiscordMessage Message { get; }

			public IReadOnlyCollection<ButtonObjectPair> Buttons { get; }


			public struct ButtonObjectPair
			{
				public ButtonObjectPair(object @object, string buttonId)
				{
					Object = @object;
					ButtonId = buttonId;
				}


				public object Object { get; }

				public string ButtonId { get; }
			}
		}
	}
}
