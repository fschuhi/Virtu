using System;
using Jellyfish.Library;

namespace Jellyfish.Virtu
{
    // Emulation of Alex Lukacz's 4play joystick card
    // http://lukazi.blogspot.com/2016/05/apple-ii-4play-joystick-card-revb.html
    // Software has been modded to use 4play, including Robotron 2084 (by Nick)
    // http://lukazi.blogspot.com/2017/08/apple-ii-4play-joystick-card-software.html
    public class JoystickCard : PeripheralCard
    {
        private enum FourPlayStateBits : uint
        {
            Up = 1 << 0, // Active High
            Down = 1 << 1, // Active High
            Left = 1 << 2, // Active High
            Right = 1 << 3, // Active High
            Trigger3 = 1 << 4, // Active High
            NotUsed = 1 << 5, // Always High
            Trigger2 = 1 << 6, // Active High
            Trigger1 = 1 << 7, // Active High
        }

        private enum AnalogThresholdPercent : uint
        {
            Min = 0,
            Max = 100,
            Mid = (Max - Min) / 2,
            Deadzone = 10,
            Up = Mid - Deadzone,
            Down = Mid + Deadzone,
            Left = Mid - Deadzone,
            Right = Mid + Deadzone,
        }

        private int getJoystickState(uint joystickNumber, bool thumbstick2 = false)
        {
            int state = (int)FourPlayStateBits.NotUsed;
            if (_joystick[joystickNumber].Valid)
            {
                _joystick[joystickNumber].UpdateAxes();
                float X, Y;
                if (thumbstick2)
                {
                    X = _joystick[joystickNumber].GetZAxisAsPercent();
                    Y = _joystick[joystickNumber].GetRAxisAsPercent();
                }
                else
                {
                    X = _joystick[joystickNumber].GetXAxisAsPercent();
                    Y = _joystick[joystickNumber].GetYAxisAsPercent();
                }
                if (X >= (uint)AnalogThresholdPercent.Right) state |= (int)FourPlayStateBits.Right;
                if (X <= (uint)AnalogThresholdPercent.Left)  state |= (int)FourPlayStateBits.Left;
                if (Y >= (uint)AnalogThresholdPercent.Down)  state |= (int)FourPlayStateBits.Down;
                if (Y <= (uint)AnalogThresholdPercent.Up)    state |= (int)FourPlayStateBits.Up;
                if ((_joystick[joystickNumber].GetButtons() & 1) != 0) state |= (int)FourPlayStateBits.Trigger1;
                if ((_joystick[joystickNumber].GetButtons() & 2) != 0) state |= (int)FourPlayStateBits.Trigger2;
            }
            return state;
        }

        private Joystick[] _joystick = new Joystick[2];

        public JoystickCard(Machine machine) : 
            base(machine)
        {
            //_joystick[0] = new Joystick(0);
            _joystick[0] = new Joystick(0, true);
            //_joystick[1] = new Joystick(1);
        }

        public override int ReadIoRegionC0C0(int address)
        {
            // read Device Select' address $C0nX; n = slot number + 8
            switch (address & 0xF)
            {
                case 0:
                    return getJoystickState(0);
                case 1:
                    return getJoystickState(0, true);
                case 2:
                    return (int)FourPlayStateBits.NotUsed;
                case 3:
                    return (int)FourPlayStateBits.NotUsed;
                default:
                    return ReadFloatingBus();
            }
        }
    }
}
