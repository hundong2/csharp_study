namespace UtilityLibrary;

public class stringLib
{
    private string stringVariable = string.Empty;
    private stringLib()
    {

    }
    private stringLib(string value)
    {
        stringVariable = value;
    }
    static public stringLib GetInstance(string value)
    {
        return new stringLib(value);
    }
    public string GetStringValue()
    {
        return stringVariable;
    }
}


