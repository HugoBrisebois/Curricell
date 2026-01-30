using System;
using System.Security.Cryptography.X509Certificates;
using Microsoft.VisualBasic;
using System.Data.SQLite;
using System.Data.SqlTypes;
using System.Data;
using System.Threading.Channels;



public class Program
{
    public static void Main()
    {
       // connect to the database
        Console.WriteLine("Connecting to database");
        string connectionString = "Data Source=Curricel.db;version=3;FailIfMissing = True";
      
       // defining the tables
       String SQLQueryCreateTable = @"CREATE TABLE IF NOT EXISTS Concepts(Id    INTEGER PRIMARY KEY, 
        Concept VARCHAR,
        Name TEXT NOT NULL,
        Description TEXT
        )";
      
        try
        {
            // Create a SQliteconnection object called connection
            using (SQLiteConnection MyConnection = new SQLiteConnection(connectionString))
            {
                MyConnection.Open(); // open a connection to database  

                // Drop the old Customers table if it exists
                using (SQLiteCommand dropCommand = new SQLiteCommand("DROP TABLE IF EXISTS Customers;", MyConnection))
                {
                    dropCommand.ExecuteNonQuery();
                }

                using (SQLiteCommand Mycommand = new SQLiteCommand(SQLQueryCreateTable, MyConnection))
                {
                    var RowsChanged = Mycommand.ExecuteNonQuery(); // execute create the table sql query
                    Console.WriteLine($"No o Rows Changes = {RowsChanged}"); // rows changed equals 0, since we are only creating a table
                } 
            } 
        }
        catch(Exception Ex)
        {
            Console.WriteLine(Ex.Message);
        }
        Console.WriteLine("Sqlite Connected");
    }



    public static void Concept()
    {
        // Defining what a concept is
        // also using a SQlite database to store all the concepts/topics as nodes like in a graph database

    }

    public static void ConceptEngine()
    {
        
    }
    
}