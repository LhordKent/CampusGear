namespace ASI.Basecode.Resources.Constants
{
    public static class RoleNames
    {
        public const string Administrator = "Administrator";
        public const string Custodian = "Custodian";
        public const string Borrower = "Borrower";
        public const string CustodianOrAdministrator = "Custodian,Administrator";
        public const string AdministratorOrCustodian = "Administrator,Custodian";
    }

    public static class BorrowingLimits
    {
        public const int MaxUnitsPerRequest = 10;
    }
}
