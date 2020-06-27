using System.Runtime.InteropServices;

namespace Jellyfish.Library
{
    public class Joystick
    {
        public Joystick(uint id, bool enableThumbstick2 = false)
        {
            Id = id;
            _joyCaps = new JOYCAPS();
            _joyInfoEx = new JOYINFOEX();
            Valid = (getCaps() == MMRESULT.JOYERR_NOERROR);
            IsThumbstick2Enabled = (Valid && enableThumbstick2 && CheckThumbStick2Available());
        }

        public bool UpdateAxes()
        {
            LastResult = getPosEx();
            return (LastResult == MMRESULT.JOYERR_NOERROR);
        }

        public uint GetButtons()
        {
            return _joyInfoEx.dwButtons;
        }

        public float GetXAxisAsPercent()
        {
            return (float)(_joyInfoEx.dwXpos - _joyCaps.wXmin) / (_joyCaps.wXmax - _joyCaps.wXmin) * 100;
        }

        public float GetYAxisAsPercent()
        {
            return (float)(_joyInfoEx.dwYpos - _joyCaps.wYmin) / (_joyCaps.wYmax - _joyCaps.wYmin) * 100;
        }

        public float GetZAxisAsPercent()
        {
            return (float)(_joyInfoEx.dwZpos - _joyCaps.wZmin) / (_joyCaps.wZmax - _joyCaps.wZmin) * 100;
        }

        public float GetRAxisAsPercent()
        {
            return (float)(_joyInfoEx.dwRpos - _joyCaps.wRmin) / (_joyCaps.wRmax - _joyCaps.wRmin) * 100;
        }

        public enum MMRESULT : uint
        {
            JOYERR_NOERROR = 0,
            JOYERR_BASE = 160,
            JOYERR_PARMS = JOYERR_BASE + 5, /* bad parameters */
            JOYERR_NOCANDO = JOYERR_BASE + 6, /* request not completed */
            JOYERR_UNPLUGGED = JOYERR_BASE + 7, /* joystick is unplugged */
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOYCAPS
        {
            public short wMid;       /* manufacturer ID */
            public short wPid;       /* product ID */
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szPname;   /* product name (NULL terminated string) */
            public uint wXmin;       /* minimum x position value */
            public uint wXmax;       /* maximum x position value */
            public uint wYmin;       /* minimum y position value */
            public uint wYmax;       /* maximum y position value */
            public uint wZmin;       /* minimum z position value */
            public uint wZmax;       /* maximum z position value */
            public uint wNumButtons; /* number of buttons */
            public uint wPeriodMin;  /* minimum message period when captured */
            public uint wPeriodMax;  /* maximum message period when captured */
            public uint wRmin;       /* minimum r position value */
            public uint wRmax;       /* maximum r position value */
            public uint wUmin;       /* minimum u (5th axis) position value */
            public uint wUmax;       /* maximum u (5th axis) position value */
            public uint wVmin;       /* minimum v (6th axis) position value */
            public uint wVmax;       /* maximum v (6th axis) position value */
            public uint wCaps;       /* joystick capabilites */
            public uint wMaxAxes;    /* maximum number of axes supported */
            public uint wNumAxes;    /* number of axes in use */
            public uint wMaxButtons; /* maximum number of buttons supported */
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szRegKey;  /* registry key */
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szOEMVxD;  /* OEM VxD in use */
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOYINFO
        {
            public uint wXpos;
            public uint wYpos;
            public uint wZpos;
            public uint wButtons;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOYINFOEX
        {
            public uint dwSize;
            public uint dwFlags;
            public uint dwXpos; // 1st axis
            public uint dwYpos; // 2nd axis
            public uint dwZpos; // 3rd axis
            public uint dwRpos; // 4th axis
            public uint dwUpos;
            public uint dwVpos;
            public uint dwButtons;
            public uint dwButtonNumber;
            public uint dwPOV;
            public uint dwReserved1;
            public uint dwReserved2;
        }

        [DllImport("winmm.dll")]
        private static extern uint joyGetDevCaps(uint uJoyID, ref JOYCAPS pjc, uint cbjc);

        [DllImport("winmm.dll")]
        private static extern uint joyGetNumDevs();

        [DllImport("winmm.dll")]
        private static extern uint joyGetPos(uint uJoyID, ref JOYINFO pji);

        [DllImport("winmm.dll"), System.Security.SuppressUnmanagedCodeSecurity]
        private static extern uint joyGetPosEx(uint uJoyID, ref JOYINFOEX pjiex);

        private MMRESULT getCaps()
        {
            return (MMRESULT)joyGetDevCaps(Id, ref _joyCaps, (uint)Marshal.SizeOf(_joyCaps));
        }

        private bool CheckThumbStick2Available()
        {
            return (_joyCaps.wZmax > 0) && (_joyCaps.wZmax > 0);
        }

        private MMRESULT getPosEx()
        {
            _joyInfoEx.dwFlags = (uint)((IsThumbstick2Enabled) ? 0x8F : 0x83); // = X, Y, Z, R, Buttons
            _joyInfoEx.dwSize = (uint)Marshal.SizeOf(_joyInfoEx);

            //#define JOY_RETURNX             0x00000001l
            //#define JOY_RETURNY             0x00000002l
            //#define JOY_RETURNZ             0x00000004l
            //#define JOY_RETURNR             0x00000008l
            //#define JOY_RETURNU             0x00000010l     /* axis 5 */
            //#define JOY_RETURNV             0x00000020l     /* axis 6 */
            //#define JOY_RETURNPOV           0x00000040l
            //#define JOY_RETURNBUTTONS       0x00000080l
            //#define JOY_RETURNRAWDATA       0x00000100l
            //#define JOY_RETURNPOVCTS        0x00000200l
            //#define JOY_RETURNCENTERED      0x00000400l
            //#define JOY_USEDEADZONE         0x00000800l
            //#define JOY_RETURNALL           (JOY_RETURNX | JOY_RETURNY | JOY_RETURNZ | \
            //                                 JOY_RETURNR | JOY_RETURNU | JOY_RETURNV | \
            //                                 JOY_RETURNPOV | JOY_RETURNBUTTONS)
            return (MMRESULT)joyGetPosEx(Id, ref _joyInfoEx);
        }
        public bool IsThumbstick2Enabled { get; }

        private JOYCAPS _joyCaps;
        private JOYINFOEX _joyInfoEx;
        public MMRESULT LastResult;
        public uint Id;
        public bool Valid;
    }
}