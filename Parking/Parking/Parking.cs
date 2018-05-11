﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Parking
{
    class Parking
    {
        private static readonly Lazy<Parking> lazy = new Lazy<Parking>(() => new Parking());
        private static List<Car> cars = new List<Car>(Settings.ParkingSpace);
        private static List<Transaction> transactions = new List<Transaction>();
        public static decimal Balance { get; private set; }
        private static System.Timers.Timer aTimerForCollectPayment, aTimerForWriteInLog;

        private Parking()
        {
            Balance = 0;
            Parking.SetTimerForCollectPayment();
            Parking.SetTimerForWriteInLog();
        }

        public static Parking GetParking() => lazy.Value;

        public decimal GetTotalRevenue() => Balance;

        private static void SetTimerForCollectPayment()
        {
            aTimerForCollectPayment = new System.Timers.Timer(Settings.Timeout);
            aTimerForCollectPayment.Elapsed += OnTimedEventForCollectPayment;
            aTimerForCollectPayment.AutoReset = true;
            aTimerForCollectPayment.Enabled = true;
        }

        private static async void OnTimedEventForCollectPayment(Object source, ElapsedEventArgs e)
        {
            foreach (var car in cars)
            {
                await CollectPaymentAsync(car);
            }
        } 

        public static async Task CollectPaymentAsync(Car car)
        {
            Settings.prices.TryGetValue(car.CarType, out var price);
            if (car.Balance < price)
            {
                price = price * Settings.CoefficientFine;
            }
            car.Balance -= price;
            Balance += price;
            transactions.Add(new Transaction(DateTime.Now, car.Id, price));
        }

        public void AddCar(CarType type, decimal balance) => cars.Add(new Car(type, balance));

        public bool HasFine(int number) => cars[number - 1].Balance < 0;

        public void RemoveCar(int number, out decimal fine)
        {
            fine = cars[number - 1].Balance;
            if (HasFine(number))
            {
                TopUp(number, Math.Abs(cars[number - 1].Balance));
                CollectPaymentAsync(cars[number - 1]);
            }
            cars.Remove(cars[number - 1]);
        }
        public decimal TopUp(int value, decimal money) => cars[value - 1].Balance += money;

        public int GetNumberOfFreePlaces() => cars == null ? Settings.ParkingSpace : Settings.ParkingSpace - cars.Count;

        public int GetNumberOfBusyPlaces() => cars?.Count ?? 0;

        public static decimal AmountForTheLastMinute() => transactions.Sum(n => n.Amount);

        public List<Transaction> GetTransactionsForTheLastMinute() => transactions;

        private static void SetTimerForWriteInLog()
        {
            aTimerForWriteInLog = new System.Timers.Timer(60000);
            aTimerForWriteInLog.Elapsed += OnTimedEventForWriteInLog;
            aTimerForWriteInLog.AutoReset = true;
            aTimerForWriteInLog.Enabled = true;
        }

        private static async void OnTimedEventForWriteInLog(Object source, ElapsedEventArgs e) => await WriteToTransactionsFileAsync();

        public static async Task WriteToTransactionsFileAsync()
        {
            try
            {
                byte[] array = Encoding.Default.GetBytes("" + DateTime.Now + " " + AmountForTheLastMinute() + " " + transactions.Count + " ");
                transactions.Clear();
                using (var fstream = new FileStream(@"C:\Users\Eugene\Documents\GitHub\Parking\Transactions.log", FileMode.OpenOrCreate))
                {
                    fstream.Seek(0, SeekOrigin.End);
                    await fstream.WriteAsync(array, 0, array.Length);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        public string GetTransactionsFile()
        {
            try
            {
                using (FileStream fstream = File.OpenRead(@"C:\Users\Eugene\Documents\GitHub\Parking\Transactions.log"))
                {
                    byte[] array = new byte[fstream.Length];             
                    fstream.Read(array, 0, array.Length); 
                    var textFromFile = Encoding.Default.GetString(array); 
                    return textFromFile;
                }
            }
            catch (FileNotFoundException)
            {
                return null;
            }
        }
    }
}
