namespace Interface;
using System.Collections;


class Hardware
{

}
class USB
{
    string name;
    public USB(string name) {this.name = name;}
    public override string ToString()
    {
        return name;
    }
}
class Notebook: Hardware, IEnumerable
{
    USB[] usbList = new USB[] {new USB("USB1"), new USB("USB2")};
    public IEnumerator GetEnumerator() //IEnumerable 
    {
        return new USBEmuerator(usbList);
    }
    public class USBEmuerator : IEnumerator //IEnumerator 
    {
        int pos = -1;
        int length = 0;
        object[] list;
        public USBEmuerator(USB[] usb)
        {
            list = usb;
            length = usb.Length;
        }
        public object Current 
        {
            get { return list[pos]; }
        }
        public bool MoveNext()
        {
            if( pos >= length -1 )
            {
                return false;
            }
            pos++;
            return true;
        }
        public void Reset()
        {
            pos = -1;
        }
    }
}
class IntegerCompare : IComparer
{
    //this method called from Array.Sort method 
    public int Compare(object x, object y)
    {
        int xValue = (int)x;
        int yValue = (int)y;

        if( xValue > yValue) return -1; //descending for 
        else if ( xValue == yValue) return 0;

        return 1;
    }
}
class Program
{
    /// <summary>
    /// IComparer Example
    /// </summary>
    public static void compareTest()
    {
        int[] intArray = new int[] {1, 2, 3, 4, 5, 6, 7};

        Array.Sort(intArray, new IntegerCompare());
        foreach(int item in intArray)
        {
            Console.WriteLine(item + ", ");
        }
    }
    public static void IEnumerableTest()
    {
        int[] intArray = new int[] {1,2,3,4,5,6};
        IEnumerator enumerator = intArray.GetEnumerator();
        while(enumerator.MoveNext()) //if no more enumerate then returned false 
        {
            Console.Write(enumerator.Current + ", ");
        }
        foreach( int item in intArray) //easy method for IEnumerable type forloop
        {
            Console.Write(item + ",");
        }
        Console.WriteLine("");

        string name = "Korea";
        foreach( var item in name)
        {
            Console.Write(item + ", ");
        }
    }
    public static void Test()
    {
        Notebook notebook = new Notebook();
        foreach( var usb in notebook)
        {
            Console.Write(usb);
        }
    }
    static void Main(string[] args)
    {
        Console.WriteLine("1. IComparer Interface Example");
        compareTest();

        Console.WriteLine("2. IEnumerable Interface Example");
        IEnumerableTest();

        Console.WriteLine("3. Test");
        Test();
        Console.WriteLine("Hello, World!");
    }
}
