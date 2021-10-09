using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CGZBot2.Tools
{
	class MessagesDialogSource
	{
		private readonly List<DialogMessage> messages = new();
		private readonly List<StateMachine<MessageUID>.StateTransitFactory<DialogContext>> transits = new();


		public event Action<DialogContext> MessageChanged;


		public void OnMessageChangedTo(Action<DialogContext> handler, MessageUID targetState)
		{
			MessageChanged += (m) => { if (m.Dialog.Machine.CurrentState == targetState) handler(m); };
		}

		public void AddMessage(DialogMessage msg)
		{
			messages.Add(msg);
		}

		public void AddTransit(Func<DialogContext, ITransitWorker<MessageUID>> worker, MessageUID startUid, MessageUID targetUid)
		{
			transits.Add(new StateMachine<MessageUID>.StateTransitFactory<DialogContext>() { WorkerFactory = worker, OriginState = startUid, TargetState = targetUid});
		}

		public MessagesDialog Start(DiscordChannel channel, DiscordMember member)
		{
			var md = new MessagesDialog(channel, member, transits, messages, this);
			md.Machine.StateChanged += (m) => { MessageChanged?.Invoke(md.Context); };

			return md;
		}
	}

	class MessagesDialog
	{
		private readonly Dictionary<MessageUID, DialogMessage> messages;


		public MessagesDialog(DiscordChannel channel, DiscordMember member, IReadOnlyCollection<StateMachine<MessageUID>.StateTransitFactory<DialogContext>> transits, IReadOnlyCollection<DialogMessage> messages, MessagesDialogSource source)
		{
			Context = new DialogContext() { Caller = member, Channel = channel, Dialog = this };
			this.messages = messages.ToDictionary(s => s.Uid);
			Source = source;

			foreach (var transit in transits)
				Machine.CreateTransit(transit.Fabricate(Context));

			Machine.StateChanged += Machine_StateChanged;
			Machine.SetStartState(MessageUID.StartMessage);

			CurrentMessage = this.messages[MessageUID.StartMessage];
			CurrentMessage.Show(Context);

			Machine.Run();
		}


		private void Machine_StateChanged(StateMachine<MessageUID> obj)
		{
			CurrentMessage.Close(Context);
			if (obj.CurrentState == MessageUID.EndDialog) return;
			CurrentMessage = messages[obj.CurrentState];
			CurrentMessage.Show(Context);
		}


		public MessagesDialogSource Source { get; }

		public DialogMessage CurrentMessage { get; private set; }

		public DialogContext Context { get; }

		public StateMachine<MessageUID> Machine { get; } = new();
	}

	class DialogMessage
	{
		private readonly Action<DialogContext, ShowContext> showAction;
		private readonly Action<DialogContext, ShowContext> closeAction;
		private readonly Dictionary<DialogContext, ShowContext> ctxs = new Dictionary<DialogContext, ShowContext>();


		public MessageUID Uid { get; }


		public DialogMessage(MessageUID uid, Action<DialogContext, ShowContext> showAction, Action<DialogContext, ShowContext> closeAction)
		{
			Uid = uid;
			this.showAction = showAction;
			this.closeAction = closeAction;
		}


		public void Show(DialogContext ctx)
		{
			ctxs.Add(ctx, new ShowContext());
			showAction(ctx, ctxs[ctx]);
		}

		public void Close(DialogContext ctx)
		{
			closeAction(ctx, ctxs[ctx]);
		}


		public class ShowContext
		{
			public IDictionary<string, object> DynamicParameters { get; } = new Dictionary<string, object>();
		}
	}

	class DialogContext
	{
		public DiscordChannel Channel { get; init; }

		public DiscordMember Caller { get; init; }

		public MessagesDialog Dialog { get; init; }

		public IDictionary<string, object> DynamicParameters { get; } = new Dictionary<string, object>();
	}

	public enum MessageUID
	{
		StartMessage = 0,
		EndDialog = -1
	}
}
