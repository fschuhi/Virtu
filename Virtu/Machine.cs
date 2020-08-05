using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        public void Initialize( object argument ) {

            _debugService = Services.GetService<DebugService>();
            _storageService = Services.GetService<StorageService>();

            foreach (var component in Components) {
                DebugMessage( "Initializing machine '{0}'", component.GetType().Name );
                component.Initialize();
            }
        }

        public void Uninitialize( object argument ) {
            foreach (var component in Components) {
                DebugMessage( "Uninitializing machine '{0}'", component.GetType().Name );
                component.Uninitialize();
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


        public void Reset( object argument ) {
            foreach (var component in Components) {
                DebugMessage( "Resetting machine '{0}'", component.GetType().Name );
                component.Reset();
                //DebugMessage("Reset machine '{0}'", component.GetType().Name);
            }
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
