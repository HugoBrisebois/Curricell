using System;
using System.Security.Cryptography.X509Certificates;

public class Program
{
    public static void Main()
    {
        Console.WriteLine("Hello, World!\n");

        Concept();
    }



    public static void Concept()
    {
        // Dictionary to hold all the information
        Dictionary<int, string> Topics= new Dictionary<int, string>();

        // adding topics
        Topics.Add(1, "Math");
        Topics.Add(2, "English");
        Topics.Add(3, "French");
        Topics.Add(4, "Science");
        Topics.Add(5, "History");

        // Display the dictionary contents

        foreach (var item in Topics)
        {
            Console.WriteLine($"{item}\n");
        }
    }

    public static void ConceptEngine()
    {

    }
    
}  