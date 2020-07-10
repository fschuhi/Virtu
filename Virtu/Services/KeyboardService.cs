using System.Diagnostics;

namespace Jellyfish.Virtu.Services {
    public abstract class KeyboardService : MachineService {
        protected KeyboardService( Machine machine ) :
            base( machine ) {
        }

        public abstract bool IsKeyDown( int key );

        public virtual void Update() {
            Debug.Assert( false, "KeyboardService.Update() not used anymore -- now implemented in VirtuRoCWpfKeyboardService.cs" );
        }

        public bool IsAnyKeyDown { get; protected set; }
        public bool IsControlKeyDown { get; protected set; }
        public bool IsShiftKeyDown { get; protected set; }

        public bool IsOpenAppleKeyDown { get; protected set; }
        public bool IsCloseAppleKeyDown { get; protected set; }

        protected bool IsResetKeyDown { get; set; }
    }
}
