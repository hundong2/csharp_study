using System.Text;
using System.Xml.Linq;

namespace ConsoleApp;

class Program
{
    static void Main()
    {
        string txt = @"<People>
        <Person name='anders' age='47' />
        <Person name='bill' age='38' />
        <Person name='charlie' age='35' />"
        + "</People>";
        StringReader sr = new StringReader(txt);
        var xml = XElement.Load(sr);
        var query = from person in xml.Elements("Person")
                    where (int)person.Attribute("age") > 40
                    select person;
        foreach (var item in query)
        {
            Console.WriteLine(item);
        }
        //output:
        //<Person name="anders" age="47" />
    }
}