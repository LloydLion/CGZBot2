using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CGZBot2.Tools
{
	class StateMachine<TState> : IDisposable, IStateMachineReporter<TState> where TState : Enum
	{
		private TState currentState;
		private TState hardSetState;
		private NotUniqueDictionary<TState, StateTransit> transits = new();
		private List<StateTransitRecord> transitRecords = new();
		private bool canceled = false;
		private Task stateUpdateTask;
		private bool disposed;


		public TState CurrentState { get { return currentState; } }

		public bool IsCanceled { get { return canceled; } }

		public IReadOnlyCollection<StateTransitRecord> TransitRecords { get { return transitRecords; } }

		public IReadOnlyCollection<StateTransit> Transits => transits.AsDictionary().SelectMany(s => s.Value).ToList();

		public object SyncRoot { get; } = new object();


		public StateMachine()
		{
			stateUpdateTask = new Task(() =>
			{
				while (!disposed)
				{
					Thread.Sleep(200);
					UpdateState();
				}
			});
		}


		public event Action<StateMachine<TState>> StateChanged;

		event Action<IStateMachineReporter<TState>> IStateMachineReporter<TState>.StateChanged 
			{ add { StateChanged += new Action<StateMachine<TState>>(value); } remove { StateChanged -= new Action<StateMachine<TState>>(value); } }


		public void ChangeStateHard(TState newState)
		{
			hardSetState = newState;
		}

		public void Run(TState startState)
		{
			lock(SyncRoot)
			{
				hardSetState = currentState = startState;

				foreach (var transit in Transits)
					transit.Worker.Start(transit, this);

				stateUpdateTask.Start();
			}
		}

		public void OnStateChangedTo(Action<StateMachine<TState>> handler, TState targetState)
		{
			StateChanged += (m) => { if (m.CurrentState.Equals(targetState)) handler(m); };
		}

		void IStateMachineReporter<TState>.OnStateChangedTo(Action<IStateMachineReporter<TState>> handler, TState targetState) => OnStateChangedTo(handler, targetState);

		public void CreateTransit(ITransitWorker<TState> worker, TState origin, TState target)
		{
			transits.Add(origin, new StateTransit() { Worker = worker, OriginState = origin, TargetState = target });
		}

		public void UpdateState()
		{
			lock(SyncRoot)
			{
				UpdateStateDirect();
			}
		}

		public Task WaitForStateAsync(TState state)
		{
			return Task.Run(() => { do { lock (SyncRoot) { } Thread.Sleep(50); } while (!CurrentState.Equals(state)); });
		}

		private void UpdateStateDirect()
		{
			if(!hardSetState.Equals(currentState))
			{
				transitRecords.Add(new StateTransitRecord()
					{ Transit = null, LocalTime = DateTime.Now, OriginState = currentState, TargetState = hardSetState });
				currentState = hardSetState;
				StateChanged?.Invoke(this);
			}

			var trans = transits.Get(currentState);
			foreach (var tran in trans)
			{
				if(tran.Worker.ReadyToTransit)
				{
					transitRecords.Add(new StateTransitRecord()
						{ Transit = tran, LocalTime = DateTime.Now, OriginState = tran.OriginState, TargetState = tran.TargetState });
					hardSetState = currentState = tran.TargetState;
					foreach (var u in trans) u.Worker.Reset();
					StateChanged?.Invoke(this);
					UpdateStateDirect();
					return;
				}
			}
		}

		public void Dispose()
		{
			disposed = true;
		}


		public struct StateTransit
		{
			public ITransitWorker<TState> Worker { get; init; }

			public TState OriginState { get; init; }

			public TState TargetState { get; init; }
		}

		public struct StateTransitRecord
		{
			public StateTransit? Transit { get; init; }

			public DateTime LocalTime { get; init; }

			public TState OriginState { get; init; }

			public TState TargetState { get; init; }
		}
	}

	interface ITransitWorker<TState> where TState : Enum
	{
		bool ReadyToTransit { get; }

		void Reset();

		void Start(StateMachine<TState>.StateTransit transit, StateMachine<TState> machine);
	}

	interface IStateMachineReporter<TState> : IDisposable where TState : Enum
	{
		TState CurrentState { get; }

		event Action<IStateMachineReporter<TState>> StateChanged;

		void OnStateChangedTo(Action<IStateMachineReporter<TState>> handler, TState targetState);
	}

	class TaskTransitWorker<TState> : ITransitWorker<TState> where TState : Enum
	{
		private readonly Func<Task> waitTaskFactory;
		private readonly bool singleTransit;
		private bool transited;
		private Task currentTask;
		private StateMachine<TState>.StateTransit transit;
		private StateMachine<TState> machine;


		public TaskTransitWorker(Func<Task> waitTaskFactory, bool singleTransit = false)
		{
			this.waitTaskFactory = waitTaskFactory;
			this.singleTransit = singleTransit;
		}


		public bool ReadyToTransit { get; private set; } = false;


		public void Reset()
		{
			ReadyToTransit = false;
			if(!(singleTransit && transited)) ReCreateTask();
		}

		public void Start(StateMachine<TState>.StateTransit transit, StateMachine<TState> machine)
		{
			this.transit = transit;
			this.machine = machine;
			ReCreateTask();
		}

		private void ReCreateTask()
		{
			var task = waitTaskFactory();

			currentTask = Task.Run(() =>
			{
				machine.WaitForStateAsync(transit.OriginState).Wait();
				task.Start();
				task.Wait();
			});

			currentTask.ContinueWith(s => { if (currentTask.IsCompletedSuccessfully) ReadyToTransit = true; transited = true; });
		}
	}
}
