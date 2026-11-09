using CampusGear.Models;
namespace CampusGear.Data.QueryModels;
public sealed record DeskData(int Pending, int ReadyToRelease, int Overdue, int DueToday);
public sealed record ValueCount<T>(T Key, int Count);
