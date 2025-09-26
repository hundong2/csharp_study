#!/usr/bin/evn dotnet-script
#r "nuget: IronPython, 3.4.1"
#r "nuget: IronPython.StdLib, 3.4.1"

using IronPython.Hosting;

var scriptEngine = Python.CreateEngine();
string code = @"print('Hello from IronPython')";
scriptEngine.Execute(code);


//using c# code dynamic, call python function
var scriptScope = scriptEngine.CreateScope();

string code2 = @"
def AddFunc(a, b):
    print('AddFunc called')
    return (a + b)
";

scriptEngine = Python.CreateEngine();
scriptScope = scriptEngine.CreateScope();
scriptEngine.Execute(code2, scriptScope);
dynamic addFunc = scriptScope.GetVariable("AddFunc");
int nResult = addFunc(5, 10);
Console.WriteLine("nResult: " + nResult);

scriptEngine = Python.CreateEngine();
scriptScope = scriptEngine.CreateScope();
//csharp variable pass to python example
List<string> list = new List<string>();
scriptScope.SetVariable("myList", list);
string code3 = @"
myList.Add('my')
myList.Add('name')
";
scriptEngine.Execute(code3, scriptScope);
foreach(var item in list)
{
    Console.WriteLine(item);
}
