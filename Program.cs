using System;

internal class Program
{
    private static void Main(string[] args)
    {
        int processId = 3456;

        MemoryReader reader = new MemoryReader(processId);

        try
        {
            reader.ReadAllMemory();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
        finally
        {
            reader.Close();
        }

        Console.ReadKey();
    }
}