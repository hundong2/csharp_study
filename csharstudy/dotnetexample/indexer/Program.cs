namespace indexer;

class IntegerText
{
    char[] txtNumber;
    public IntegerText(int number)
    {
        //Int32 to System.String, and System.String to char array
        this.txtNumber = number.ToString().ToCharArray();
    }
    public char this[int index]
    {
        get
        {
            return txtNumber[txtNumber.Length - index - 1];
        }
        set
        {
            txtNumber[txtNumber.Length - index - 1] = value;
        }
    }
    public override string ToString()
    {
        return new string(txtNumber);
    }
    public int ToInt32()
    {
        return Int32.Parse(ToString());
    }
}
class Program
{
    static void Main(string[] args)
    {
        IntegerText aInt = new IntegerText(123456);
        int step = 1;
        for(int i = 0; i < aInt.ToString().Length; i++)
        {
            Console.WriteLine($"digit of {step}: {aInt[i]}");
            step+=10;
        }
        aInt[3] = '5';
        Console.WriteLine($"{aInt.ToInt32()}");
    }
}
