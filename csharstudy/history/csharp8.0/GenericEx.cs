namespace ConsoleApp;

public class Program
{
    public static void Main()
    {
        {
            Action action = () => Console.WriteLine("action");
            ActionProxy<Action> proxy = new ActionProxy<Action>(action);
            proxy.Call();
        }

        {
            Action<int> action = (arg) => Console.WriteLine($"action {arg}");
            ActionProxy<Action<int>> proxy = new ActionProxy<Action<int>>(action);
            proxy.Call();
        }

    }
    class ActionProxy<T> where T : System.Delegate
    {
        T _callbackFunc;
        public ActionProxy(T callbackFunc)
        {
            _callbackFunc = callbackFunc;
        }
        public void Call()
        {
            switch (_callbackFunc)
            {
                case Action action:
                    action();
                    break;
                case Action<int> action:
                    action(5);
                    break;
            }
        }
    }
}