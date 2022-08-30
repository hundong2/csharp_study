using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace SortingOrderingLists
{
    public partial class MainWindow : Window
    {
        //TODO: Step 4: Add attribute my list
        List<int> MyList;
        public MainWindow()
        {
            InitializeComponent();
            //TODO: Step 5: Initialize my list
            MyList = new List<int>() { 4,5,6,3,2,1,7,8,9 };
            //TODO: Step 7: Set the output textblock's content to
            //MyList
            MyTextBlock.Text = StringifyList(MyList);
        }
        //TODO: Step 6: Write the stringify list method
        //Results the contents of the list in a comma
        //separated string format
        public string StringifyList(List<int> inList)
        {
            string accum = "";
            foreach (int item in inList)
            {
                accum += item.ToString() + ",";
                //remove the last comma, simplest
                //possible implementation
                if (item == inList.Last())
                    accum = accum.TrimEnd(',');
            }
            return accum;
        }
        //TOOD: Step 7: Write the where method to only include odd numbers
        //You can explain that this is accomplished by dividing by 2 and looking
        //at the remainder. If the reminder is NOT 0 then the object will be added
        //to the result list. Finally we want this to be returned in a string format
        //so that we can easily display it
        public string FilterListOddNumbers(List<int> inList)
        {
            return StringifyList(inList.Where(i => (i % 2) != 0).ToList());
        }
        //TODO: Step 8: Same logic as with odd numbers only this time we want to
        //include the even numbers only
        public string FilterListEvenNumbers(List<int> inList)
        {
            return StringifyList(inList.Where(i => (i % 2) == 0).ToList());
        }
        //TODO: Step 9: Filter the list when the button is pressed
        private void Odd_Click(object sender, RoutedEventArgs e)
        {
            MyTextBlock.Text = FilterListOddNumbers(MyList);
        }
        //TODO: Step 10: Filter the list when the button is pressed
        private void Even_Click(object sender, RoutedEventArgs e)
        {
            MyTextBlock.Text = FilterListEvenNumbers(MyList);
        }
        //TODO: Step 11: Remove the filter set to list to original
        private void RemoveFilter_Click(object sender, RoutedEventArgs e)
        {
            MyTextBlock.Text = StringifyList(new List<int>() { 4, 5, 6, 3, 2, 1, 7, 8, 9 });
        }
    }
}
