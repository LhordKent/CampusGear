namespace CampusGear.Models;
public static class BorrowingLimits { public const int MaxUnitsPerRequest = 10; }
public enum ItemStatus { Available, Reserved, Borrowed, ForRepair, Inactive }
