namespace UtilityLibrary;

public class exampleAdvancedTrick
{
    private exampleAdvancedTrick()
    {

    }

    public static exampleAdvancedTrick GetInstance()
    {
        return new exampleAdvancedTrick();
    }

    public (int sum, int minus) GetCalculate(int a, int b )
    {
        return ((a+b), (a - b));
    }
}
