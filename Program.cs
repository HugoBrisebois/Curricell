using System;
using System.Security.Cryptography.X509Certificates;
using Microsoft.VisualBasic;
using System.Data.SQLite;
using System.Data.SqlTypes;
using System.Data;



public class Program
{
    public static void Main()
    {
       // connect to the database
        Console.WriteLine("Connecting to database");

        string connectionString = "Data Source=Curricel.db;version=3;";
        using var connection= new SQLiteConnection(connectionString);
        connection.Open();
        
        Console.WriteLine("Sqlite Connected");
        String SQLQueryCreateTable = @"CREATE TABLE IF NOT EXISTS Concepts(Id INTEGER PRIMARY KEY,
        Name TEXT NOT NULL,
        Topic STRING,
        Description TEXT
        )";

        using var commend= new SQLiteCommand(SQLQueryCreateTable, connection);
        commend.ExecuteNonQuery();
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