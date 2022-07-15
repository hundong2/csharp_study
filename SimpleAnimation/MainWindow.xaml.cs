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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SimpleAnimation
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            //TODO: Step 4: Create double animation
            //Make sure to include the reference using System.Windows.Media.Animation;
            DoubleAnimation fadeAnimation = new DoubleAnimation();
            fadeAnimation.Duration = TimeSpan.FromSeconds(1.0d);
            //TODO: Step 5: Here you can place around with the values
            //from = 0 and to = 1 is fade-in animation
            //from = 1 and to = 0 is fade-out aniamtion
            fadeAnimation.From = 0.0d;
            fadeAnimation.To = 1.0d;
            //TODO: Step 6: Launch the animation
            MainGrid.BeginAnimation(Grid.OpacityProperty, fadeAnimation);
        }
    }
}
