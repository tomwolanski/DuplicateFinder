using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DuplicateFinder.Scanner
{
    /// <summary>
    /// Interaction logic for ScannerWindow.xaml
    /// </summary>
    public partial class ScannerWindow : Window
    {
        private readonly ScannerVM _vm = new ScannerVM();

        public ScannerWindow()
        {
            DataContext = _vm;
            InitializeComponent();

            Loaded += (s, e) => _vm.OnLoad();
            Closed += (s, e) => _vm.OnClose();

            _vm.CloseRequested += (s, e) => Close();
        }
    }
}
