using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Tesla Rental Platform Initialized");
        DatabaseInitializer.Initialize();

        var carService = new TeslaCarService();
        var rentalService = new RentalService();

        
        carService.AddTeslaCar(new TeslaCar { Model = "Model 3", HourlyRate = 50, PerKmRate = 0.8 });

        
        var cars = carService.GetAllTeslaCars();
        Console.WriteLine("Available cars:");
        foreach (var car in cars)
        {
            Console.WriteLine($"ID: {car.ID}, Model: {car.Model}, Hourly Rate: {car.HourlyRate}, Per Km Rate: {car.PerKmRate}");
        }

        rentalService.StartRental(clientId: 1, carId: 1);  
        rentalService.EndRental(rentalId: 1, kilometersDriven: 150);
    }
}

class TeslaCar
{
    public int ID { get; set; }
    public string Model { get; set; }
    public double HourlyRate { get; set; }
    public double PerKmRate { get; set; }
}

class TeslaCarService
{
    private const string ConnectionString = "Data Source=TeslaRental.db";

    public void AddTeslaCar(TeslaCar car)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO TeslaCars (Model, HourlyRate, PerKmRate) 
            VALUES ($model, $hourlyRate, $perKmRate);
        ";
        command.Parameters.AddWithValue("$model", car.Model);
        command.Parameters.AddWithValue("$hourlyRate", car.HourlyRate);
        command.Parameters.AddWithValue("$perKmRate", car.PerKmRate);

        command.ExecuteNonQuery();
    }

    public List<TeslaCar> GetAllTeslaCars()
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT ID, Model, HourlyRate, PerKmRate FROM TeslaCars;";

        var cars = new List<TeslaCar>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            cars.Add(new TeslaCar
            {
                ID = reader.GetInt32(0),
                Model = reader.GetString(1),
                HourlyRate = reader.GetDouble(2),
                PerKmRate = reader.GetDouble(3)
            });
        }
        return cars;
    }
}

class RentalService
{
    private const string ConnectionString = "Data Source=TeslaRental.db";

    public void StartRental(int clientId, int carId)
{
    using var connection = new SqliteConnection(ConnectionString);
    connection.Open();

    
    var checkClientCommand = connection.CreateCommand();
    checkClientCommand.CommandText = "SELECT COUNT(*) FROM Clients WHERE ID = $clientId;";
    checkClientCommand.Parameters.AddWithValue("$clientId", clientId);
    var clientExists = Convert.ToInt32(checkClientCommand.ExecuteScalar()) > 0;

    
    var checkCarCommand = connection.CreateCommand();
    checkCarCommand.CommandText = "SELECT COUNT(*) FROM TeslaCars WHERE ID = $carId;";
    checkCarCommand.Parameters.AddWithValue("$carId", carId);
    var carExists = Convert.ToInt32(checkCarCommand.ExecuteScalar()) > 0;

    if (!clientExists)
    {
        throw new Exception("Invalid CarID. Ensure exist before starting a rental.");
    }
    if (!carExists)
    {
        throw new Exception("Invalid ClientID. Ensure exist before starting a rental.");
    }


    
    var command = connection.CreateCommand();
    command.CommandText = @"
        INSERT INTO Rentals (ClientID, CarID, StartTime) 
        VALUES ($clientId, $carId, $startTime);
    ";
    command.Parameters.AddWithValue("$clientId", clientId);
    command.Parameters.AddWithValue("$carId", carId);
    command.Parameters.AddWithValue("$startTime", DateTime.UtcNow.ToString("o"));

    command.ExecuteNonQuery();
}

    public void EndRental(int rentalId, double kilometersDriven)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        
        var getRentalCommand = connection.CreateCommand();
        getRentalCommand.CommandText = @"
            SELECT StartTime, HourlyRate, PerKmRate 
            FROM Rentals 
            JOIN TeslaCars ON Rentals.CarID = TeslaCars.ID 
            WHERE Rentals.ID = $rentalId;
        ";
        getRentalCommand.Parameters.AddWithValue("$rentalId", rentalId);

        DateTime startTime;
        double hourlyRate, perKmRate;

        using (var reader = getRentalCommand.ExecuteReader())
        {
            if (!reader.Read())
                throw new Exception("Rental not found.");

            startTime = DateTime.Parse(reader.GetString(0));
            hourlyRate = reader.GetDouble(1);
            perKmRate = reader.GetDouble(2);
        }

        
        var endTime = DateTime.UtcNow;
        var rentalHours = (endTime - startTime).TotalHours;
        var totalAmount = (rentalHours * hourlyRate) + (kilometersDriven * perKmRate);

        
        var updateRentalCommand = connection.CreateCommand();
        updateRentalCommand.CommandText = @"
            UPDATE Rentals 
            SET EndTime = $endTime, KilometersDriven = $kilometersDriven, TotalAmount = $totalAmount
            WHERE ID = $rentalId;
        ";
        updateRentalCommand.Parameters.AddWithValue("$endTime", endTime.ToString("o"));
        updateRentalCommand.Parameters.AddWithValue("$kilometersDriven", kilometersDriven);
        updateRentalCommand.Parameters.AddWithValue("$totalAmount", totalAmount);
        updateRentalCommand.Parameters.AddWithValue("$rentalId", rentalId);

        updateRentalCommand.ExecuteNonQuery();

        Console.WriteLine($"Rental {rentalId} ended. Total amount: €{totalAmount:F2}");
    }
}

class DatabaseInitializer
{
    private const string ConnectionString = "Data Source=TeslaRental.db";

    public static void Initialize()
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS TeslaCars (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                Model TEXT NOT NULL,
                HourlyRate REAL NOT NULL,
                PerKmRate REAL NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Clients (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Email TEXT NOT NULL UNIQUE
            );

            CREATE TABLE IF NOT EXISTS Rentals (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                ClientID INTEGER NOT NULL,
                CarID INTEGER NOT NULL,
                StartTime TEXT NOT NULL,
                EndTime TEXT,
                KilometersDriven REAL,
                TotalAmount REAL,
                FOREIGN KEY(ClientID) REFERENCES Clients(ID),
                FOREIGN KEY(CarID) REFERENCES TeslaCars(ID)
            );
        ";
        command.ExecuteNonQuery();
    }
}
