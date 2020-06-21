using System;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Jellyfish.Virtu
{
    public class InstructionProfile : IComparable<InstructionProfile>
    {
        public long Count;
        public string Name;
        public int Opcode;

        public int CompareTo(InstructionProfile other)
        {
            return this.Count.CompareTo(other.Count);
        }
    }

    public class MemoryLocation : IComparable<MemoryLocation>
    {
        public int Address;
        public int Length;
        public long Count;

        public int CompareTo(MemoryLocation other)
        {
            return this.Count.CompareTo(other.Count);
        }
    }

    public partial class MemoryWindow : Window
    {
        public MemoryWindow(Machine machine)
            : this()
        {
            _machine = machine;
        }

        public MemoryWindow()
        {
            InitializeComponent();
            if (!DesignerProperties.GetIsInDesignMode(this))
            {
                _image.Source = _bitmap;
                //_image.Stretch = Stretch.None;

                _image.MouseMove += new MouseEventHandler(ImageMouseMove);
                _image.MouseLeftButtonDown += new MouseButtonEventHandler(ImageMouseLeftButton);
                _image.MouseRightButtonDown += new MouseButtonEventHandler(ImageMouseRightButton);

                //MouseWheel += new MouseWheelEventHandler(WindowMouseWheel);
                _resetButton.Click += (sender, e) => OnResetButtonClick();

                this.KeyDown += new KeyEventHandler(MemoryWindow_KeyDown);

                CompositionTarget.Rendering += OnCompositionTargetRendering;
            }
        }

        void MemoryWindow_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Down:
                    DebugAddressChangeByKeyboard(0x100);
                    break;

                case Key.Left:
                    DebugAddressChangeByKeyboard(-1);
                    break;

                case Key.Right:
                    DebugAddressChangeByKeyboard(1);
                    break;

                case Key.Up:
                    DebugAddressChangeByKeyboard(-0x100);
                    break;
            }
        }

        private void OnResetButtonClick()
        {
            _machine.Memory.DebugReset();
        }

        private void ImageMouseMove(object sender, MouseEventArgs e)
        {
            _image.Cursor = Cursors.Cross;
            if ((e.LeftButton == MouseButtonState.Pressed) ||
                (e.RightButton == MouseButtonState.Pressed))
            {
                _debugAddress = GetMouseAddress(e);
                UpdateDebugAddress();

                if (e.RightButton == MouseButtonState.Pressed)
                    UpdateDebugDump();
            }
        }

        private void ImageMouseLeftButton(object sender, MouseButtonEventArgs e)
        {
            _image.Cursor = Cursors.Cross;
            _debugAddress = GetMouseAddress(e);
            UpdateDebugAddress();
        }

        private void ImageMouseRightButton(object sender, MouseButtonEventArgs e)
        {
            _image.Cursor = Cursors.Cross;
            _debugAddress = GetMouseAddress(e);
            UpdateDebugAddress();
            UpdateDebugDump();
        }

        private void DebugAddressChangeByKeyboard(int delta)
        {
            _image.Cursor = Cursors.None;
            _debugAddressCursorVisible = true;
            _debugAddress += delta;
            _debugAddress &= 0xFFFF;
            UpdateDebugWindow();
        }

        int GetMouseAddress(MouseEventArgs e)
        {
            double xScale = _image.MinWidth / _image.ActualWidth;
            int x = (int)(e.GetPosition(_image).X * xScale);
            double yScale = _image.MinHeight / _image.ActualHeight;
            int y = (int)(e.GetPosition(_image).Y * yScale);
            return (y * 0x100) + x;
        }

        void UpdateDebugWindow()
        {
            UpdateDebugAddress();
            UpdateDebugDump();
        }

        void UpdateDebugAddress()
        {
            int value = _machine.Memory.ReadDebug(_debugAddress);
            _addressText.Text = String.Format("{0:X4}:{1:X2}", _debugAddress, value);
        }

        void UpdateDebugDump()
        {
            var dump = new StringBuilder();
            int startAddress = _debugAddress - 8;
            int endAddress = _debugAddress + 8;
            for (int address = startAddress; address <= endAddress; address++)
            {
                if (address < 0 || address > 0xFFFF)
                    dump.Append("\r\n");
                else
                    dump.Append(String.Format("{0:X4}:{1:X2} R:{2:X4} W:{3:X4}\r\n", address,
                        _machine.Memory.ReadDebug(address),
                        _machine.Memory.DebugInfo[address].LastReadFrom,
                        _machine.Memory.DebugInfo[address].LastWriteFrom));
            }
            _dumpText.Text = dump.ToString();
            var disasm = new StringBuilder();
            if (_machine.Memory.DebugInfo[_debugAddress].ExecCount > 0)
            {
                int instructionsBack = 0;
                int lowestAddress = _debugAddress - (3 * 9);
                for (int address = _debugAddress; address >= lowestAddress; address--)
                {
                    if (address < 0 || _machine.Memory.DebugInfo[address].ExecCount == 0)
                    {
                        break;
                    }
                    if (_machine.Memory.DebugInfo[address].Flags.HasFlag(DebugFlags.Opcode))
                    {
                        startAddress = address;
                        instructionsBack += 1;
                        if (instructionsBack == 9)
                            break;
                    }
                }
                int instructionsDisassembled = 0;
                for (int address = startAddress; ; )
                {
                    if (instructionsDisassembled >= 17 ||
                        !_machine.Memory.DebugInfo[address].Flags.HasFlag(DebugFlags.Opcode))
                    {
                        break;
                    }
                    disasm.AppendFormat("{0:X4}- ", address);
                    address += _machine.Memory.Disassemble(address, disasm);
                    if (address > 0xFFFF)
                        break;
                    disasm.Append("\r\n");
                    instructionsDisassembled += 1;
                }
            }
            _disassembleText.Text = disasm.ToString();
        }

        private void WindowMouseWheel(object sender, MouseWheelEventArgs e)
        {
            System.Windows.Media.Matrix matrix = _image.RenderTransform.Value;

            if (e.Delta > 0)
            {
                matrix.ScaleAt(
                    1.5,
                    1.5,
                    e.GetPosition(this).X,
                    e.GetPosition(this).Y);
            }
            else
            {
                matrix.ScaleAt(
                    1.0 / 1.5,
                    1.0 / 1.5,
                    e.GetPosition(this).X,
                    e.GetPosition(this).Y);
            }

            _image.RenderTransform = new MatrixTransform(matrix);
        }

        private uint CalculateColourFromCycles(long nowCycles, long accessCycles)
        {
            uint fadeMin = (History) ? (uint)0x40 : (uint)0x00;
            long fadeTime = 3 * 1000 * 1000;

            uint value = fadeMin;
            long readAge = nowCycles - accessCycles;
            if (readAge < fadeTime)
                value = (uint)(((double)(fadeTime - readAge) / (double)fadeTime) * (double)(0xFF - fadeMin)) + fadeMin;
            return value;
        }

        private void OnCompositionTargetRendering(object sender, EventArgs e)
        {
            if (Visibility != Visibility.Visible)
                return;

            long time = DateTime.UtcNow.Ticks;
            if (time - _lastRenderingTime < TimeSpan.TicksPerSecond / 20)
                return; // limit refresh to 20 FPS
            _lastRenderingTime = time;

            long nowCycles = _machine.Cpu.Cycles + 1;
            for (int address = 0; address < BitmapWidth * BitmapHeight; ++address)
            {
                uint pixel = 0;
                if (_machine.Memory.DebugInfo[address].ReadCount > 0)
                {
                    uint value = CalculateColourFromCycles(nowCycles, _machine.Memory.DebugInfo[address].LastReadCycle);
                    pixel |= value << 8;
                }
                if (_machine.Memory.DebugInfo[address].WriteCount > 0)
                {
                    uint value = CalculateColourFromCycles(nowCycles, _machine.Memory.DebugInfo[address].LastWriteCycle);
                    pixel |= value << 16;
                }
                if (_machine.Memory.DebugInfo[address].ExecCount > 0)
                {
                    uint value = CalculateColourFromCycles(nowCycles, _machine.Memory.DebugInfo[address].LastExecCycle);
                    pixel |= value;
                }
                _pixels[address] = pixel;
            }
            if (_debugAddressCursorVisible)
                _pixels[_debugAddress] ^= 0xFFFFFF;
            _bitmap.WritePixels(BitmapRect, _pixels, BitmapStride, 0);

            _frameCount++;
            if (time - _lastUpdateTime >= TimeSpan.TicksPerSecond)
            {
                _lastUpdateTime = time;
                _frameRateText.Text = String.Format("{0:D} fps", _frameCount);
                _frameCount = 0;
                _debugAddressCursorVisible ^= true;
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        public bool History
        {
            get { return (bool)GetValue(HistoryProperty); }
            set { SetValue(HistoryProperty, value); }
        }

        public static readonly DependencyProperty HistoryProperty =
            DependencyProperty.Register("History", typeof(bool), typeof(MemoryWindow), new UIPropertyMetadata(false));

        private Machine _machine;

        private const int BitmapWidth = 256;
        private const int BitmapHeight = 256;
        private const int BitmapDpi = 96;
        private static readonly PixelFormat BitmapPixelFormat = PixelFormats.Bgr32;
        private static readonly int BitmapStride = (BitmapWidth * BitmapPixelFormat.BitsPerPixel + 7) / 8;
        private static readonly Int32Rect BitmapRect = new Int32Rect(0, 0, BitmapWidth, BitmapHeight);

        private WriteableBitmap _bitmap = new WriteableBitmap(BitmapWidth, BitmapHeight, BitmapDpi, BitmapDpi, BitmapPixelFormat, null);
        private uint[] _pixels = new uint[BitmapWidth * BitmapHeight];

        private int _debugAddress;
        private bool _debugAddressCursorVisible;
        private int _frameCount;
        private long _lastUpdateTime;
        private long _lastRenderingTime;
    }
}
