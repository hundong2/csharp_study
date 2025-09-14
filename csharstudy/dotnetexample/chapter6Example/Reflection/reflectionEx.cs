using System.Reflection;

class Program
{
    static void Main(string[] args)
    {
        AppDomain currentDomain = AppDomain.CurrentDomain;
        Console.WriteLine("Current Domain: " + currentDomain.FriendlyName);
        foreach (var asm in currentDomain.GetAssemblies())
        {
            Console.WriteLine("Assembly: " + asm.FullName);
            foreach (var mdoule in asm.GetModules())//assembly에 포함 된 모듈 정보
            {
                Console.WriteLine("Module: " + mdoule.FullyQualifiedName);
                foreach( var type in mdoule.GetTypes()) //모듈에 포함 된 타입 정보
                {
                    Console.WriteLine("Type: " + type.FullName);
                    foreach(var ctorInfo in type.GetConstructors()) //타입에 포함 된 생성자 정보
                    {
                        Console.WriteLine("Constructor: " + ctorInfo.Name);
                    }
                    foreach(var evenInfo in type.GetEvents()) //타입에 포함 된 이벤트 정보
                    {
                        Console.WriteLine("Event: " + evenInfo.Name);
                    }
                    foreach(var field in type.GetFields()) //타입에 포함 된 필드 정보
                    {
                        Console.WriteLine("Field: " + field.Name + ", " + field.FieldType);
                    }
                    foreach (var prop in type.GetProperties()) //타입에 포함 된 속성 정보
                    {
                        Console.WriteLine("Property: " + prop.Name + ", " + prop.PropertyType);
                    }
                    foreach (var member in type.GetMembers()) //타입에 포함 된 멤버 정보
                        {
                            Console.WriteLine("Member: " + member.Name + ", " + member.MemberType);
                        }
                }
            }
        }
    }
}