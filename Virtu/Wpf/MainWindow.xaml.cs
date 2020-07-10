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
    }
}
