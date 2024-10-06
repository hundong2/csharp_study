using System.Collections;

namespace ExampleHash
{
    public class ExampleHashClass
    {
        private int _key = 0; 
        public class ExampleHash()
        {

        }

        public void ExampleHashTable()
        {
            Hashtable ht = new Hashtable();
            ht.Add("1", "Hello");
            ht.Add("2", "World");
            ht.Add("3", "Anderson");

            Console.WriteLine(ht["1"]);
            Console.WriteLine(ht["2"]);
            Console.WriteLine(ht["3"]);
            ht.Remove("3");
        }
    }
}

