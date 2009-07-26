﻿using Jellyfish.Virtu.Services;

namespace Jellyfish.Virtu
{
    public sealed class Speaker : MachineComponent
    {
        public Speaker(Machine machine) :
            base(machine)
        {
        }

        public override void Initialize()
        {
            _audioService = Machine.Services.GetService<AudioService>();
        }

        public void ToggleOutput()
        {
            _audioService.ToggleOutput();
        }

        private AudioService _audioService;
    }
}
