using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using Jellyfish.Library;
using Jellyfish.Virtu.Services;

namespace Jellyfish.Virtu {
    public enum MachineState { Stopped = 0, Starting, Running, Pausing, Paused, Stopping }

    public class Machine : IDisposable {
        public Machine( MainPage mainPage ) {
            MainPage = mainPage;

            Events = new MachineEvents( this );
            Services = new MachineServices();

            Cpu = new Cpu( this );
            Memory = new Memory( this );
            Keyboard = new Keyboard( this );
            GamePort = new GamePort( this );
            Cassette = new Cassette( this );
            Speaker = new Speaker( this );
            Video = new Video( this );
            NoSlotClock = new NoSlotClock( this );

            var emptySlot = new PeripheralCard( this );
            Slot1 = emptySlot;
            Slot2 = emptySlot;
            Slot3 = emptySlot;
            Slot4 = emptySlot;
            Slot5 = emptySlot;
            Slot6 = new DiskIIController( this );
            Slot7 = emptySlot;

            Slots = new Collection<PeripheralCard> { null, Slot1, Slot2, Slot3, Slot4, Slot5, Slot6, Slot7 };
            Components = new Collection<MachineComponent> { Cpu, Memory, Keyboard, GamePort, Cassette, Speaker, Video, NoSlotClock, Slot1, Slot2, Slot3, Slot4, Slot5, Slot6, Slot7 };

            BootDiskII = Slots.OfType<DiskIIController>().Last();

            MachineThread = new Thread( RunMachineThread ) { Name = "Machine" };
            MachineThread.IsBackground = true;
        }

        public void Dispose() {
            _pausedEvent.Close();
            _unpausedEvent.Close();
        }

        private void DebugMessage( string format, params object[] args ) {
            if (_debugService == null) {
                // suppress the message
                // we are probably before the machine thread has started
            } else {
                _debugService.WriteMessage( format, args );
            }
        }

        #region Initialize / Uninitialize
        private void Initialize() {
            foreach (var component in Components) {
                DebugMessage( "Initializing machine '{0}'", component.GetType().Name );
                component.Initialize();
                //DebugMessage("Initialized machine '{0}'", component.GetType().Name);
            }
        }

        private void Uninitialize() {
            foreach (var component in Components) {
                DebugMessage( "Uninitializing machine '{0}'", component.GetType().Name );
                component.Uninitialize();
                //DebugMessage("Uninitialized machine '{0}'", component.GetType().Name);
            }
        }
        #endregion


        #region load/save state
        private void LoadStateFromStream( Stream stream ) {
            using (var reader = new BinaryReader( stream )) {
                string signature = reader.ReadString();
                var version = new Version( reader.ReadString() );

                // avoid state version mismatch (for now)
                if ((signature != StateSignature) || (version != new Version( Machine.Version ))) {
                    throw new InvalidOperationException();
                }
                foreach (var component in Components) {
                    DebugMessage( "Loading machine '{0}'", component.GetType().Name );
                    component.LoadState( reader, version );
                    //DebugMessage("Loaded machine '{0}'", component.GetType().Name);
                }
            }
        }

        private void SaveStateToStream( Stream stream ) {
            using (var writer = new BinaryWriter( stream )) {
                writer.Write( StateSignature );
                writer.Write( Machine.Version );
                foreach (var component in Components) {
                    DebugMessage( "Saving machine '{0}'", component.GetType().Name );
                    component.SaveState( writer );
                    //DebugMessage("Saved machine '{0}'", component.GetType().Name);
                }
            }
        }

        private void LoadState() {
            // #if WINDOWS
#if BLA
            var args = Environment.GetCommandLineArgs();
            if (args.Length > 1)
                {
                    string name = args[1];
                    Func<string, Action<Stream>, bool> loader = StorageService.LoadFile;

                if (name.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
                {
                    name = name.Substring(6);
                    loader = StorageService.LoadResource;
                }

                if (name.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
                {
                    loader(name, stream => LoadState(stream));
                }
                else if (name.EndsWith(".prg", StringComparison.OrdinalIgnoreCase))
                {
                    loader(name, stream => Memory.LoadPrg(stream));
                }
                else if (name.EndsWith(".xex", StringComparison.OrdinalIgnoreCase))
                {
                    loader(name, stream => Memory.LoadXex(stream));
                }
                else
                {
                    loader(name, stream => BootDiskII.BootDrive.InsertDisk(name, stream, false));
                }
            }
            else
#endif
            // if (!_storageService.Load(Machine.StateFileName, stream => LoadState(stream)))
            if (true) {
                StorageService.LoadResource( "Disks/Default.dsk", stream => BootDiskII.BootDrive.InsertDisk( "Default.dsk", stream, false ) );
            }
        }

        public void LoadStateFromFile( string name ) {
            StorageService.LoadFile( name, stream => LoadStateFromStream( stream ) );
        }
        private void SaveStateToStore() {
            SaveStateToStore( Machine.StateFileName );
        }
        public void SaveStateToStore( string name ) {
            _storageService.Save( name, stream => SaveStateToStream( stream ) );
        }
        public void LoadStateFromStore( string name ) {
            _storageService.Load( name, stream => LoadStateFromStream( stream ) );
        }
        public void SaveStateToFile( string name ) {
            StorageService.SaveFile( name, stream => SaveStateToStream( stream ) );
        }

        #endregion


        public void Reset() {
            foreach (var component in Components) {
                DebugMessage( "Resetting machine '{0}'", component.GetType().Name );
                component.Reset();
                //DebugMessage("Reset machine '{0}'", component.GetType().Name);
            }
        }

        public void StartMachineThread() {
            _debugService = Services.GetService<DebugService>();
            _storageService = Services.GetService<StorageService>();

            DebugMessage( "Starting machine" );
            State = MachineState.Starting;
            MachineThread.Start();
        }

        public void Pause() {
            if (State != MachineState.Running) return;

            DebugMessage( "Pausing machine" );
            State = MachineState.Pausing;

            if (! IsInMachineThread()) {
                DebugMessage( "waiting for Machine to signal Paused" );
                _pausedEvent.WaitOne();
                DebugMessage( "machine signaled Paused" );
            }
        }

        public void Unpause() {
            // Machine starts Stopped, so make sure we can issue the initializing Unpause
            if (State != MachineState.Paused && State != MachineState.Stopped) {
                DebugMessage( "Unpause skipped (not in Paused or Stopped)" );
                return;
            } else {
                DebugMessage( "signal Unpaused" );
                _unpausedEvent.Set();
            }
        }

        public void StopMachineThread() {
            if (State == MachineState.Stopped) return;

            DebugMessage( "Stopping machine" );
            State = MachineState.Stopping;

            // machine might be paused, waiting to be unpaused
            _unpausedEvent.Set();

            // true if this thread has been started and has not terminated normally or aborted; otherwise, false.
            if (MachineThread.IsAlive) {
                // Blocks the calling thread until the thread represented by this instance terminates, while continuing to perform standard COM and SendMessage pumping.
                MachineThread.Join();
            }
            State = MachineState.Stopped;
            DebugMessage( "Stopped machine" );
        }

        public bool IsInMachineThread() {
            return Thread.CurrentThread == MachineThread;
        }

        private void RunMachineThread() {
            Initialize();
            Reset();

            // load last state by default, see SaveState below
            //LoadState();
            //LoadState( "bla.bin " );

            DebugMessage( "initialized, reset, loaded state" );
            // we don't start execution of 6502 code right away
            State = MachineState.Paused;
            _pausedEvent.Set();

            // we can use the Paused state to load a different state, or insert boot disk, then set breakpoints etc.

            // IMPORTANT: Machine must be unpaused manually
            _unpausedEvent.WaitOne();
            _pausedEvent.Reset();

            // we are now officially running
            State = MachineState.Running;
            DebugMessage( "machine running (in RunMachineThread)" );

            bool isBreakpointAtRPC = false;

            do {
                do {
                    if (isBreakpointAtRPC) {
                        // do not break yet again on the same RPC
                        DebugMessage( "maching skipping handled breakpoint" );
                        isBreakpointAtRPC = false;
                        Events.HandleEvents( Cpu.Execute() );
                    } else {
                        if (Memory.DebugInfo[Cpu.RPC].Flags.HasFlag( DebugFlags.Breakpoint )) {
                            DebugMessage( "machine encountered breakpoint" );
                            // it's a breakpoint, so pause the machine
                            isBreakpointAtRPC = true;
                            State = MachineState.Pausing;
                        } else {
                            Events.HandleEvents( Cpu.Execute() );
                        }
                    }
                }
                while (State == MachineState.Running);

                if (State == MachineState.Pausing) {

                    // signal that we have reached Paused state
                    State = MachineState.Paused;
                    _pausedEvent.Set();
                    DebugMessage( "machine Paused" );
                     MainPage.Dispatcher.Send( () => MainPage.OnPause() );

                    // SaveState( "bla.bin " );

                    DebugMessage( "machine now waiting for Unpause" );

                    // to continue, either Unpause() or Stop()
                    _unpausedEvent.WaitOne();
                    _pausedEvent.Reset();
                    if (State != MachineState.Stopping)
                        MainPage.Dispatcher.Send( () => MainPage.OnUnpause() );

                    DebugMessage( "machine unpaused" );

                    // stopping of the machine can also be triggered while being paused
                    // in this case we must not transition to Running
                    if (State != MachineState.Stopping) {

                        // was Unpause(), so it's safe to continue execution
                        State = MachineState.Running;
                    }

                }
            }
            while (State != MachineState.Stopping);

            DebugMessage( "machine has exited main Cpu.Execute loop, saving state" );

            // by default save the current state (see LoadState above)
            // SaveState();
            Uninitialize();

            // indiscriminately set events to release anyone still listening
            _unpausedEvent.Set();
            _pausedEvent.Set();

            DebugMessage( "machine exits RunMachineThread" );
        }

        public const string Version = "0.9.4.0";

        public MainPage MainPage { get; private set; }

        public MachineEvents Events { get; private set; }
        public MachineServices Services { get; private set; }
        public MachineState State { get { return _state; } private set { _state = value; } }

        public Cpu Cpu { get; private set; }
        public Memory Memory { get; private set; }
        public Keyboard Keyboard { get; private set; }
        public GamePort GamePort { get; private set; }
        public Cassette Cassette { get; private set; }
        public Speaker Speaker { get; private set; }
        public Video Video { get; private set; }
        public NoSlotClock NoSlotClock { get; private set; }

        public PeripheralCard Slot1 { get; private set; }
        public PeripheralCard Slot2 { get; private set; }
        public PeripheralCard Slot3 { get; private set; }
        public PeripheralCard Slot4 { get; private set; }
        public PeripheralCard Slot5 { get; private set; }
        public PeripheralCard Slot6 { get; private set; }
        public PeripheralCard Slot7 { get; private set; }

        public Collection<PeripheralCard> Slots { get; private set; }
        public Collection<MachineComponent> Components { get; private set; }

        public DiskIIController BootDiskII { get; private set; }

        public Thread MachineThread { get; private set; }

        private const string StateFileName = "State.bin";
        private const string StateSignature = "Virtu";

        private DebugService _debugService;
        public DebugService DebugService { get { return _debugService; } }

        private StorageService _storageService;
        private volatile MachineState _state;

        private ManualResetEvent _pausedEvent = new ManualResetEvent( false );
        private AutoResetEvent _unpausedEvent = new AutoResetEvent( false );

        public void WaitForPaused() {
            _pausedEvent.WaitOne();
        }
    }
}
