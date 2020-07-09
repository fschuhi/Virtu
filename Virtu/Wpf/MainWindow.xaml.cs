using Jellyfish.Virtu.Services;
using System.Windows;
using System.Windows.Controls;

namespace Jellyfish.Virtu {
    public partial class MainWindow : Window {

        public MainWindow() {
            InitializeComponent();
        }

        public MainPage GetMainPage() {
            return _mainPage;
        }

        protected override void OnClosing( System.ComponentModel.CancelEventArgs e ) {
            //do my stuff before closing
            _mainPage.Machine.StopMachineThread();

            base.OnClosing( e );
        }
    }
}
