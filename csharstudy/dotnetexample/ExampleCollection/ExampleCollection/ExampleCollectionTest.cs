using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExampleCollection
{
    public class ExampleCollectionTest
    {
        public ExampleCollectionTest()
        {

        }
        /// <summary>
        /// FIFO Structure 
        /// </summary>
        public static void ExampleStack()
        {
            Console.WriteLine("ExampleStack");
            System.Collections.Stack st = new System.Collections.Stack(); 
            st.Push(1);
            st.Push(2);
            st.Push("4");

            var last = st.Pop();
            Console.WriteLine(last);
            st.Push(7);
            while(st.Count > 0)
            {
                Console.WriteLine(st.Pop());
            }
        }
        public static void ExampleQueue()
        {
            Console.WriteLine("ExampleQueue");
            System.Collections.Queue q = new System.Collections.Queue();
            q.Enqueue(1);
            q.Enqueue(2);
            q.Enqueue(3);
            q.Enqueue(4);
            q.Enqueue(5);
            q.Enqueue(6);
            q.Enqueue(7);
            q.Enqueue(8);
            q.Enqueue(9);
            q.Enqueue(10);
            while (q.Count > 0)
            {
                Console.WriteLine(q.Dequeue());
            }
        }
    }
}
