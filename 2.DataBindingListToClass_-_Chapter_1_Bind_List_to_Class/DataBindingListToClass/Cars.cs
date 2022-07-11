using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataBindingListToClass
{
    public class Cars
    {
        //TODO: Step 5: Write GetCars method that
        //just returns a list of cars
        public static List<Car> GetCars()
        {
            //TODO: Step 6: Return a list of cars
            //Make sure to write ToList() at the end
            //GOTO: MainWindow.xaml
            return new List<Car>()
            {
                new Car(){Owner="Mike", Type=CarType.Hatchback},
                new Car(){Owner="Emma", Type=CarType.Sedan},
                new Car(){Owner="Jon", Type=CarType.SUV}
            }.ToList();
        }
    }
}
