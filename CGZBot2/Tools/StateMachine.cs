using Microsoft.Extensions.Logging;
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
		private Task stateUpdateTask;
		private bool disposed;
		private bool started;


		public TState CurrentState { get { return currentState; } }

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
			if (started) throw new InvalidOperationException("Can't start machine twice");

			lock(SyncRoot)
			{
				SetStartState(startState);
				Run();
			}
		}

		public void Run()
		{
			if (started) throw new InvalidOperationException("Can't start machine twice");

			lock (SyncRoot)
			{
				foreach (var transit in Transits)
					transit.Worker.Start(transit, this);

				stateUpdateTask.Start();

				started = true;
			}
		}

		public void SetStartState(TState state)
		{
			if (started) throw new InvalidOperationException("Can't set start state after start");

			lock (SyncRoot)
			{
				hardSetState = currentState = state;
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
		
		public void CreateTransit(StateTransit transit)
		{
			transits.Add(transit.OriginState, transit);
		}

		public void UpdateState()
		{
			lock(SyncRoot)
			{
				UpdateStateDirect();
			}
		}

		public Task WaitForStateAsync(TState state, CancellationToken token = default)
		{
			return Task.Run(() => { do { lock (SyncRoot) { } Thread.Sleep(50); } while (!CurrentState.Equals(state) && !token.IsCancellationRequested); }, token);
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
				bool rtt = false;
				try
				{
					rtt = tran.Worker.ReadyToTransit;
				}
				catch(Exception ex)
				{
					Program.Client.Logger.Log(LogLevel.Warning, ex, "Exception in state machine while getting transit status of {0}", tran.GetType().FullName);
				}


				if(rtt)
				{
					transitRecords.Add(new StateTransitRecord()
						{ Transit = tran, LocalTime = DateTime.Now, OriginState = tran.OriginState, TargetState = tran.TargetState });
					hardSetState = currentState = tran.TargetState;

					foreach (var u in trans)
					{
						try
						{
							u.Worker.Reset();
						}
						catch(Exception ex)
						{
							Program.Client.Logger.Log(LogLevel.Warning, ex, "Exception in state machine while resetting transit worker {0}", tran.GetType().FullName);
						}
					}

					try
					{
						StateChanged?.Invoke(this);
					}
					catch(Exception ex)
					{
						Program.Client.Logger.Log(LogLevel.Warning, ex, "Exception in event handler of state machine (switching to state - {0})", CurrentState.ToString());
					}

					UpdateStateDirect();
					return;
				}
			}
		}

		public void Dispose()
		{
			disposed = true;
		}

		public struct StateTransitFactory<TParam>
		{
			public Func<TParam, ITransitWorker<TState>> WorkerFactory { get; init; }

			public TState OriginState { get; init; }

			public TState TargetState { get; init; }


			public StateTransit Fabricate(TParam param)
			{
				return new StateTransit() { OriginState = OriginState, TargetState = TargetState, Worker = WorkerFactory(param) };
			}
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
		private readonly Func<CancellationToken, Task> waitTaskFactory;
		private readonly bool singleTransit;
		private bool transited;
		private Task currentTask;
		private CancellationTokenSource cts;
		private StateMachine<TState>.StateTransit transit;
		private StateMachine<TState> machine;


		public TaskTransitWorker(Func<CancellationToken, Task> waitTaskFactory, bool singleTransit = false)
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
			if(cts != null) cts.Cancel();
			cts = new CancellationTokenSource();
			var token = cts.Token;

			var task = waitTaskFactory(token);

			currentTask = Task.Run(() =>
			{
				machine.WaitForStateAsync(transit.OriginState, token).Wait();
				if (token.IsCancellationRequested) return;

				if(task.Status == TaskStatus.Created) task.Start();

				task.Wait();

				if(task.IsCompletedSuccessfully)
				{
					ReadyToTransit = true;
					transited = true;
				}
			}, token);
		}
	}

	class PredicateTransitWorker<TState> : ITransitWorker<TState> where TState : Enum
	{
		private readonly Predicate<StateMachine<TState>> predicate;
		private StateMachine<TState> machine;


		public PredicateTransitWorker(Predicate<StateMachine<TState>> predicate)
		{
			this.predicate = predicate;
		}


		public bool ReadyToTransit => predicate(machine);


		public void Reset()
		{
			
		}

		public void Start(StateMachine<TState>.StateTransit transit, StateMachine<TState> machine)
		{
			this.machine = machine;
		}
	}

	class InvertedTransitWorker<TState> : ITransitWorker<TState> where TState : Enum
	{
		private readonly ITransitWorker<TState> worker;


		public bool ReadyToTransit => !worker.ReadyToTransit;


		public InvertedTransitWorker(ITransitWorker<TState> worker)
		{
			this.worker = worker;
		}


		public void Reset() => worker.Reset();

		public void Start(StateMachine<TState>.StateTransit transit, StateMachine<TState> machine) => worker.Start(transit, machine);
	}
}
