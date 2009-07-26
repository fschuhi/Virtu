﻿using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Jellyfish.Virtu.Services;

namespace Jellyfish.Virtu
{
    public sealed partial class MainPage : UserControl, IDisposable
    {
        public MainPage()
        {
            InitializeComponent();

            _storageService = new SilverlightStorageService(_machine);
            _keyboardService = new SilverlightKeyboardService(_machine, this);
            _gamePortService = new GamePortService(_machine); // not connected
            _audioService = new SilverlightAudioService(_machine, this, _media);
            _videoService = new SilverlightVideoService(_machine, _image);

            _machine.Services.AddService(typeof(StorageService), _storageService);
            _machine.Services.AddService(typeof(KeyboardService), _keyboardService);
            _machine.Services.AddService(typeof(GamePortService), _gamePortService);
            _machine.Services.AddService(typeof(AudioService), _audioService);
            _machine.Services.AddService(typeof(VideoService), _videoService);

            Loaded += (sender, e) => _machine.Start();
            CompositionTarget.Rendering += CompositionTarget_Rendering;
            Application.Current.Exit += (sender, e) => _machine.Stop();

            _disk1Button.Click += (sender, e) => DiskButton_Click(0);
            _disk2Button.Click += (sender, e) => DiskButton_Click(1);
        }

        public void Dispose()
        {
            _machine.Dispose();
            _storageService.Dispose();
            _keyboardService.Dispose();
            _gamePortService.Dispose();
            _audioService.Dispose();
            _videoService.Dispose();
        }

        private void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            _keyboardService.Update();
            _gamePortService.Update();
            _videoService.Update();
        }

        private void DiskButton_Click(int drive)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "Disk Files (*.dsk;*.nib)|*.dsk;*.nib|All Files (*.*)|*.*";

            bool? result = dialog.ShowDialog();
            if (result.HasValue && result.Value)
            {
                using (FileStream stream = dialog.File.OpenRead())
                {
                    _machine.Pause();
                    _machine.DiskII.Drives[drive].InsertDisk(dialog.File.Name, stream, false);
                    _machine.Unpause();
                }
            }
        }

        private Machine _machine = new Machine();

        private StorageService _storageService;
        private KeyboardService _keyboardService;
        private GamePortService _gamePortService;
        private AudioService _audioService;
        private VideoService _videoService;
    }
}
