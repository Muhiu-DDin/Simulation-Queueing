using queueanalysis.Models;

namespace queueanalysis.Services;

public class DistributionParams
{
    public string Type { get; set; } = "exponential"; // poisson, exponential, normal, uniform
    public double? Rate { get; set; } // For Poisson/Exponential (lambda/mu)
    public double? Mean { get; set; } // For Normal
    public double? StdDev { get; set; } // For Normal
    public double? A { get; set; } // For Uniform (min)
    public double? B { get; set; } // For Uniform (max)
}

public class CustomerRecord
{
    public int Id { get; set; }
    public double RandomNum { get; set; }
    public double Cp { get; set; }
    public double InterArrival { get; set; }
    public double ArrivalTime { get; set; }
    public double ServiceTime { get; set; }
    public double ServiceRandomNum { get; set; }
    public double StartTime { get; set; }
    public double EndTime { get; set; }
    public double TurnAround { get; set; }
    public double WaitTime { get; set; }
    public double ResponseTime { get; set; }
    public int ServerId { get; set; }
}

public class ServerTask
{
    public int CustomerId { get; set; }
    public double Start { get; set; }
    public double End { get; set; }
}

public class ServerRecord
{
    public int Id { get; set; }
    public double BusyTime { get; set; }
    public double Utilization { get; set; }
    public List<ServerTask> Tasks { get; set; } = new();
}

public class CPTableEntry
{
    public int X { get; set; }
    public double Prob { get; set; }
    public double Cp { get; set; }
}

public class QueueLengthPoint
{
    public double Time { get; set; }
    public int QueueLength { get; set; }
}

public class UtilizationPoint
{
    public double Time { get; set; }
    public double Utilization { get; set; }
}

public class SimulationResultExtended
{
    public List<CustomerRecord> Customers { get; set; } = new();
    public List<ServerRecord> Servers { get; set; } = new();
    public List<QueueLengthPoint> QueueLengthOverTime { get; set; } = new();
    public List<UtilizationPoint> ServerUtilizationOverTime { get; set; } = new();
    public Averages Averages { get; set; } = new();
    public int TotalArrivals { get; set; }
    public int TotalServed { get; set; }
    public List<CPTableEntry> CpTable { get; set; } = new();
}

public class SimulationEngine
{
    private readonly Random _random = new();

    // Factorial function
    private double Factorial(int n)
    {
        if (n <= 1) return 1;
        double result = 1;
        for (int i = 2; i <= n; i++) result *= i;
        return result;
    }

    // Generate Poisson probability distribution table with CP values
    private List<CPTableEntry> GeneratePoissonCPTable(double lambda)
    {
        var table = new List<CPTableEntry>();
        double cumulativeProb = 0;
        int k = 0;
        double expNegLambda = Math.Exp(-lambda);

        while (cumulativeProb <= 1 && k < 50)
        {
            double pmf = (expNegLambda * Math.Pow(lambda, k)) / Factorial(k);
            cumulativeProb += pmf;

            table.Add(new CPTableEntry
            {
                X = k,
                Prob = Math.Round(pmf * 10000) / 10000,
                Cp = Math.Round(cumulativeProb * 10000) / 10000
            });
            k++;

            double cp = Math.Round(cumulativeProb * 10000) / 10000;
            if (Math.Abs(cp - 1) < 0.0001) break;
        }

        return table;
    }

    // Lookup inter-arrival time from CP table using random number
    private (double value, double cp) LookupFromCPTable(List<CPTableEntry> table, double randomNum)
    {
        foreach (var entry in table)
        {
            if (randomNum <= entry.Cp)
            {
                return (entry.X, entry.Cp);
            }
        }
        return (table[^1].X, 1);
    }

    // Random number from Poisson distribution
    private double PoissonRandom(double lambda)
    {
        double L = Math.Exp(-lambda);
        int k = 0;
        double p = 1;

        do
        {
            k++;
            p *= _random.NextDouble();
        } while (p > L);

        return k - 1;
    }

    // Z-Table for Normal distribution
    private readonly List<(double prob, double z)> _zTable = new()
    {
        (0.0001, -3.72), (0.0005, -3.29), (0.001, -3.09), (0.005, -2.58), (0.01, -2.33),
        (0.02, -2.05), (0.025, -1.96), (0.03, -1.88), (0.04, -1.75), (0.05, -1.645),
        (0.06, -1.555), (0.07, -1.48), (0.08, -1.41), (0.09, -1.34), (0.10, -1.28),
        (0.15, -1.04), (0.20, -0.84), (0.25, -0.67), (0.30, -0.52), (0.35, -0.39),
        (0.40, -0.25), (0.45, -0.13), (0.50, 0.00), (0.55, 0.13), (0.60, 0.25),
        (0.65, 0.39), (0.70, 0.52), (0.75, 0.67), (0.80, 0.84), (0.85, 1.04),
        (0.90, 1.28), (0.91, 1.34), (0.92, 1.41), (0.93, 1.48), (0.94, 1.555),
        (0.95, 1.645), (0.96, 1.75), (0.97, 1.88), (0.975, 1.96), (0.98, 2.05),
        (0.99, 2.33), (0.995, 2.58), (0.999, 3.09), (0.9995, 3.29), (0.9999, 3.72)
    };

    // Lookup Z value from Standard Normal Table
    private double LookupZFromTable(double R)
    {
        for (int i = 0; i < _zTable.Count - 1; i++)
        {
            if (R <= _zTable[i].prob)
                return _zTable[i].z;
            
            if (R > _zTable[i].prob && R <= _zTable[i + 1].prob)
            {
                double ratio = (R - _zTable[i].prob) / (_zTable[i + 1].prob - _zTable[i].prob);
                return _zTable[i].z + ratio * (_zTable[i + 1].z - _zTable[i].z);
            }
        }
        return _zTable[^1].z;
    }

    // Random number generators
    private double ExponentialRandom(double rate) => -Math.Log(1 - _random.NextDouble()) / rate;
    
    private double NormalRandom(double mean, double stdDev)
    {
        double R = _random.NextDouble();
        double Z = LookupZFromTable(R);
        return Math.Max(0.01, mean + Z * stdDev);
    }
    
    private double UniformRandom(double a, double b) => a + _random.NextDouble() * (b - a);
    
    private double GetRandomValue(DistributionParams parameters)
    {
        return parameters.Type switch
        {
            "poisson" => PoissonRandom(parameters.Rate ?? 1),
            "exponential" => ExponentialRandom(parameters.Rate ?? 1),
            "normal" => NormalRandom(parameters.Mean ?? 1, parameters.StdDev ?? 0.2),
            "uniform" => UniformRandom(parameters.A ?? 0.5, parameters.B ?? 1.5),
            _ => ExponentialRandom(parameters.Rate ?? 1)
        };
    }

    // Get service time based on distribution
    private (double time, double randomNum) GetServiceTime(DistributionParams parameters)
    {
        double time;
        double R = _random.NextDouble();

        switch (parameters.Type)
        {
            case "poisson":
            case "exponential":
                double mu = parameters.Rate ?? 1;
                time = -mu * Math.Log(R);
                break;
            case "normal":
                time = NormalRandom(parameters.Mean ?? 1, parameters.StdDev ?? 0.2);
                break;
            case "uniform":
                time = UniformRandom(parameters.A ?? 0.5, parameters.B ?? 1.5);
                break;
            default:
                double defaultMu = parameters.Rate ?? 1;
                time = -defaultMu * Math.Log(R);
                break;
        }
        
        return (Math.Max(1, Math.Round(time)), Math.Round(R * 10000) / 10000);
    }

    private class QueuedCustomer
    {
        public int Id { get; set; }
        public double ArrivalTime { get; set; }
        public double ServiceTime { get; set; }
        public double RemainingServiceTime { get; set; }
        public double ServiceRandomNum { get; set; }
        public double InterArrival { get; set; }
        public double RandomNum { get; set; }
        public double Cp { get; set; }
        public double? FirstStartTime { get; set; }
    }

    private class ServerState
    {
        public int Id { get; set; }
        public bool Busy { get; set; }
        public double BusyUntil { get; set; }
        public QueuedCustomer? Customer { get; set; }
        public List<ServerTask> Tasks { get; set; } = new();
    }

    public SimulationResultExtended RunSimulation(
    string arrivalType, string serviceType,
    DistributionParams arrivalParams, DistributionParams serviceParams,
    int numServers, int numCustomers = 20)
    {
        // Generate CP table for arrival distribution
        var arrivalCPTable = (arrivalType == "poisson" || arrivalType == "exponential")
            ? GeneratePoissonCPTable(arrivalParams.Rate ?? 1)
            : new List<CPTableEntry>();
        
        if (arrivalCPTable.Count > 0)
            numCustomers = arrivalCPTable.Count;

        var queue = new List<QueuedCustomer>();
        var servers = new List<ServerState>();
        for (int i = 0; i < numServers; i++)
        {
            servers.Add(new ServerState
            {
                Id = i + 1,
                Busy = false,
                BusyUntil = 0,
                Customer = null,
                Tasks = new List<ServerTask>()
            });
        }

        var customers = new List<CustomerRecord>();
        var queueLengthOverTime = new List<QueueLengthPoint>();
        var serverUtilizationOverTime = new List<UtilizationPoint>();

        // Generate all customers
        var allCustomers = new List<QueuedCustomer>();
        double currentArrivalTime = 0;

        for (int i = 0; i < numCustomers; i++)
        {
            double interArrival;
            double cp;
            double randomNum;

            if (i == 0)
            {
                interArrival = 0;
                cp = arrivalCPTable.Count > 0 ? arrivalCPTable[0].Cp : 0;
                randomNum = 0;
            }
            else
            {
                randomNum = Math.Round(_random.NextDouble() * 10000) / 10000;

                if (arrivalCPTable.Count > 0)
                {
                    var lookup = LookupFromCPTable(arrivalCPTable, randomNum);
                    interArrival = lookup.value;
                    cp = lookup.cp;
                }
                else
                {
                    interArrival = Math.Round(GetRandomValue(arrivalParams));
                    cp = randomNum;
                }
            }

            currentArrivalTime += interArrival;
            var serviceResult = GetServiceTime(serviceParams);

            allCustomers.Add(new QueuedCustomer
            {
                Id = i + 1,
                ArrivalTime = currentArrivalTime,
                ServiceTime = serviceResult.time,
                RemainingServiceTime = serviceResult.time,
                ServiceRandomNum = serviceResult.randomNum,
                InterArrival = interArrival,
                RandomNum = randomNum,
                Cp = cp
            });
        }

        // Track completed customers
        var completedCustomers = new Dictionary<int, (QueuedCustomer customer, double endTime, int serverId)>();

        // Process customers through simulation
        int customerIndex = 0;
        double currentTime = 0;

        while (customerIndex < allCustomers.Count || queue.Count > 0 || servers.Any(s => s.Busy))
        {
            // Find next event
            double nextEventTime = double.MaxValue;
            string nextEventType = "arrival";
            int departingServerIndex = -1;

            if (customerIndex < allCustomers.Count)
            {
                nextEventTime = allCustomers[customerIndex].ArrivalTime;
            }

            for (int i = 0; i < servers.Count; i++)
            {
                if (servers[i].Busy && servers[i].BusyUntil <= nextEventTime)
                {
                    nextEventTime = servers[i].BusyUntil;
                    nextEventType = "departure";
                    departingServerIndex = i;
                }
            }

            if (nextEventTime == double.MaxValue) break;

            currentTime = nextEventTime;

            queueLengthOverTime.Add(new QueueLengthPoint
            {
                Time = Math.Round(currentTime * 100) / 100,
                QueueLength = queue.Count
            });

            int busyServers = servers.Count(s => s.Busy);
            serverUtilizationOverTime.Add(new UtilizationPoint
            {
                Time = Math.Round(currentTime * 100) / 100,
                Utilization = Math.Round((busyServers / (double)numServers) * 100)
            });

            if (nextEventType == "arrival" && customerIndex < allCustomers.Count)
            {
                var customer = allCustomers[customerIndex];
                customerIndex++;

                int freeServerIndex = servers.FindIndex(s => !s.Busy);

                if (freeServerIndex != -1)
                {
                    // Start service immediately
                    var server = servers[freeServerIndex];
                    double startTime = currentTime;
                    double endTime = currentTime + customer.RemainingServiceTime;

                    customer.FirstStartTime = startTime;
                    server.Busy = true;
                    server.BusyUntil = endTime;
                    server.Customer = customer;
                    server.Tasks.Add(new ServerTask
                    {
                        CustomerId = customer.Id,
                        Start = Math.Round(startTime * 100) / 100,
                        End = Math.Round(endTime * 100) / 100
                    });
                }
                else
                {
                    queue.Add(customer);
                }
            }
            else if (nextEventType == "departure" && departingServerIndex != -1)
            {
                var server = servers[departingServerIndex];
                var finishedCustomer = server.Customer!;

                completedCustomers[finishedCustomer.Id] = (finishedCustomer, currentTime, server.Id);

                server.Busy = false;
                server.Customer = null;

                if (queue.Count > 0)
                {
                    var nextCustomer = queue[0];
                    queue.RemoveAt(0);
                    double startTime = currentTime;
                    double endTime = currentTime + nextCustomer.RemainingServiceTime;

                    if (!nextCustomer.FirstStartTime.HasValue)
                        nextCustomer.FirstStartTime = startTime;

                    server.Busy = true;
                    server.BusyUntil = endTime;
                    server.Customer = nextCustomer;
                    server.Tasks.Add(new ServerTask
                    {
                        CustomerId = nextCustomer.Id,
                        Start = Math.Round(startTime * 100) / 100,
                        End = Math.Round(endTime * 100) / 100
                    });
                }
            }
        }

        // Build final customer records
        foreach (var (id, data) in completedCustomers)
        {
            var c = data.customer;
            double startTime = c.FirstStartTime ?? c.ArrivalTime;
            double endTime = data.endTime;
            double turnAround = Math.Round((endTime - c.ArrivalTime) * 100) / 100;
            double waitTime = Math.Round((turnAround - c.ServiceTime) * 100) / 100;
            double responseTime = Math.Round((startTime - c.ArrivalTime) * 100) / 100;

            customers.Add(new CustomerRecord
            {
                Id = c.Id,
                RandomNum = c.RandomNum,
                Cp = c.Cp,
                InterArrival = c.InterArrival,
                ArrivalTime = c.ArrivalTime,
                ServiceTime = c.ServiceTime,
                ServiceRandomNum = c.ServiceRandomNum,
                StartTime = startTime,
                EndTime = endTime,
                TurnAround = turnAround,
                WaitTime = waitTime,
                ResponseTime = responseTime,
                ServerId = data.serverId
            });
        }

        customers = customers.OrderBy(c => c.Id).ToList();

        // Calculate server records
        var serverBusyTimes = servers.Select(s => s.Tasks.Sum(t => t.End - t.Start)).ToList();
        double totalSimulationTime = currentTime;

        var serverRecords = new List<ServerRecord>();
        for (int i = 0; i < servers.Count; i++)
        {
            double busy = serverBusyTimes[i];
            double util = totalSimulationTime > 0
                ? Math.Round((busy / totalSimulationTime) * 10000) / 100
                : 0;
            serverRecords.Add(new ServerRecord
            {
                Id = servers[i].Id,
                BusyTime = Math.Round(busy * 100) / 100,
                Utilization = Math.Min(100, util),
                Tasks = servers[i].Tasks
            });
        }

        // Calculate averages
        int totalCustomers = customers.Count;
        var averages = new Averages();
        if (totalCustomers > 0)
        {
            averages.InterArrival = Math.Round(customers.Average(c => c.InterArrival) * 100) / 100;
            averages.ServiceTime = Math.Round(customers.Average(c => c.ServiceTime) * 100) / 100;
            averages.TurnAround = Math.Round(customers.Average(c => c.TurnAround) * 100) / 100;
            averages.WaitTime = Math.Round(customers.Average(c => c.WaitTime) * 100) / 100;
            averages.ResponseTime = Math.Round(customers.Average(c => c.ResponseTime) * 100) / 100;
        }

        return new SimulationResultExtended
        {
            Customers = customers,
            Servers = serverRecords,
            QueueLengthOverTime = queueLengthOverTime,
            ServerUtilizationOverTime = serverUtilizationOverTime,
            Averages = averages,
            TotalArrivals = numCustomers,
            TotalServed = customers.Count,
            CpTable = arrivalCPTable
        };
    }

    // Theoretical M/M/c calculations
    public (double rho, bool stable, double Lq, double L, double Wq, double W, double P0) 
        CalculateMMcMetrics(double lambda, double mu, int c)
    {
        double rho = lambda / (c * mu);

        if (rho >= 1)
        {
            return (rho, false, double.PositiveInfinity, double.PositiveInfinity, 
                    double.PositiveInfinity, double.PositiveInfinity, 0);
        }

        double sum = 0;
        for (int n = 0; n < c; n++)
        {
            sum += Math.Pow(lambda / mu, n) / Factorial(n);
        }
        
        double lastTerm = Math.Pow(lambda / mu, c) / (Factorial(c) * (1 - rho));
        double P0 = 1 / (sum + lastTerm);
        double Lq = (P0 * Math.Pow(lambda / mu, c) * rho) / (Factorial(c) * Math.Pow(1 - rho, 2));
        double L = Lq + lambda / mu;
        double W = L / lambda;
        double Wq = Lq / lambda;

        return (Math.Round(rho, 4), true, Math.Round(Lq, 4), Math.Round(L, 4), 
                Math.Round(Wq, 4), Math.Round(W, 4), Math.Round(P0, 4));
    }
}