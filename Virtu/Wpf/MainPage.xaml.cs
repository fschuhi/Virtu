using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Jellyfish.Virtu.Services;
using Microsoft.Win32;

namespace Jellyfish.Virtu {
    public sealed partial class MainPage : UserControl, IDisposable {
        public MainPage() {
            InitializeComponent();

            Machine = new Machine( this );
        }

        public void Init( KeyboardService keyboardService) {

            _debugService = DebugService.Default;
            _storageService = new WpfStorageService( Machine );
            if ( keyboardService != null ) {
                _keyboardService = keyboardService;
            } else {
                _keyboardService = new WpfKeyboardService( Machine, this );
            }
            _gamePortService = new GamePortService( Machine ); // not connected
            _audioService = new WpfAudioService( Machine, this );
            _videoService = new WpfVideoService( Machine, this, _image );

            Machine.Services.AddService( typeof( DebugService ), _debugService );
            Machine.Services.AddService( typeof( StorageService ), _storageService );
            Machine.Services.AddService( typeof( KeyboardService ), _keyboardService );
            Machine.Services.AddService( typeof( GamePortService ), _gamePortService );
            Machine.Services.AddService( typeof( AudioService ), _audioService );
            Machine.Services.AddService( typeof( VideoService ), _videoService );

            _memoryWindow = new MemoryWindow( Machine );

            // Loaded += ( sender, e ) => Machine.StartMachineThread();
            CompositionTarget.Rendering += OnCompositionTargetRendering;

            // see VirtuRoCWpfKeyboardService.cs
            // _disk1Button.Click += ( sender, e ) => OnDiskButtonClick( 0 );
            //_disk2Button.Click += ( sender, e ) => OnDiskButtonClick( 1 );
            _memoryButton.Click += ( sender, e ) => OnMemoryButtonClick();
        }

        public void OnPause() {
            _state.Text = string.Format( CultureInfo.InvariantCulture, "paused @ PC ${0:X4}", Machine.Cpu.RPC );
        }

        public void OnUnpause() {
            _state.Text = "running";
        }

        public void Dispose() {
            Machine.Dispose();
            _debugService.Dispose();
            _storageService.Dispose();
            _keyboardService.Dispose();
            _gamePortService.Dispose();
            _audioService.Dispose();
            _videoService.Dispose();
        }

        public void WriteMessage( string message ) {
            _debugText.Text += message + Environment.NewLine;
            _debugScrollViewer.UpdateLayout();
            _debugScrollViewer.ScrollToVerticalOffset( double.MaxValue );
        }

        private void OnCompositionTargetRendering( object sender, EventArgs e ) {
            _keyboardService.Update();
            _gamePortService.Update();
            _videoService.Update();

            long time = DateTime.UtcNow.Ticks;
            if (time - _lastTime >= TimeSpan.TicksPerSecond) {
                _lastTime = time;
                long nowCycles = Machine.Cpu.Cycles;
                double cycles = nowCycles - _lastCycles;
                _lastCycles = nowCycles;

                _speedText.Text = String.Format( "{0:0.000} MHz", Math.Round( (cycles / 1000000), 3 ) );
            }
        }

        private void OnMemoryButtonClick() {
            if (_memoryWindow.Visibility == Visibility.Visible) {
                _memoryWindow.Hide();
            } else {
                _memoryWindow.Show();
            }
        }

        public Window MainWindow { get { return Window.GetWindow( this ); } }
        public Machine Machine { get; set; }

        public DebugService _debugService;
        public StorageService _storageService;
        public KeyboardService _keyboardService;
        public GamePortService _gamePortService;
        public AudioService _audioService;
        public VideoService _videoService;

        private long _lastCycles;
        private long _lastTime;

        public MemoryWindow _memoryWindow;
    }
}
