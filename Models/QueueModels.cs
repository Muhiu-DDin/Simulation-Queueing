namespace queueanalysis.Models;

public class Customer
{
    public int Id { get; set; }
    public double InterArrival { get; set; }
    public double ArrivalTime { get; set; }
    public double ServiceTime { get; set; }
    public double StartTime { get; set; }
    public double EndTime { get; set; }
    public double TurnAround { get; set; }
    public double WaitTime { get; set; }
    public double ResponseTime { get; set; }
}

public class CpEntry
{
    public double Prob { get; set; }
    public double Cp { get; set; }
}

public class ServerStat
{
    public int Id { get; set; }
    public double Utilization { get; set; }
}

public class Averages
{
    public double InterArrival { get; set; }
    public double ServiceTime { get; set; }
    public double TurnAround { get; set; }
    public double WaitTime { get; set; }
    public double ResponseTime { get; set; }
}