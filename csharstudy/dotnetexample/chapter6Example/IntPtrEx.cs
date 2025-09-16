Console.WriteLine(IntPtr.Size); 
//32-bit 4, 64-bit 8
Console.WriteLine(IntPtr.Zero); 
//0
IntPtr ptr = new IntPtr(123456);
Console.WriteLine(ptr); 
//123456
IntPtr ptr2 = new IntPtr(123456L);
Console.WriteLine(ptr2); 
//123456