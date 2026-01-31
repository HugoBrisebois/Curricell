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
    
    String SQLQueryCreateTable2 = @"CREATE TABLE IF NOT EXISTS topics(Id INTEGER PRIMARY KEY,
    Topic VARCHAR,
    Description TEXT
    )";
      
        try
        {
            // Create a SQliteconnection object called connection
            using (SQLiteConnection MyConnection = new SQLiteConnection(connectionString))
            {
                MyConnection.Open(); // open a connection to database  


                using (SQLiteCommand Mycommand = new SQLiteCommand(SQLQueryCreateTable, MyConnection))
                {
                    var RowsChanged = Mycommand.ExecuteNonQuery(); // execute create the table sql query
                    Console.WriteLine($"No o Rows Changes for concepts table = {RowsChanged}"); // rows changed equals 0, since we are only creating a table
                }

                // Execute the second table creation query
                using (SQLiteCommand Mycommand2 = new SQLiteCommand(SQLQueryCreateTable2, MyConnection))
                {
                    var RowsChanged2 = Mycommand2.ExecuteNonQuery();
                    Console.WriteLine($"No o Rows Changes for topics table = {RowsChanged2}");
                }
            } 
        }
        catch(Exception Ex)
        {
            Console.WriteLine(Ex.Message);
        }
        Console.WriteLine("Sqlite Connected");
        
        
        string insertSql = "INSERT INTO topics (topic, description) VALUES (@Topic, @Description)";
       
        using (SQLiteConnection connection = new SQLiteConnection(connectionString))
        {
            SQLiteCommand insertCommand = new SQLiteCommand(insertSql, connection); 

            // Parameters
            insertCommand.Parameters.AddWithValue("@Topic", "math");
            insertCommand.Parameters.AddWithValue("@Description", "the language of the universe");

            try
            {
                connection.Open();
                insertCommand.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                connection.Close();
                Console.WriteLine("topic added");
            }
        }
    }



    public static void Concept(SQLiteConnection connection)
    {
        // defining what is a concept
        // function to add concepts/ topics

        
        

    }

    public static void ConceptEngine()
    {
        
    }
    
}