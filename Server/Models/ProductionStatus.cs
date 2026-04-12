using Server.DAL;

// Represents fixed production status values from the database (lookup table)

namespace Server.Models
{
    public class ProductionStatus
    {
        public int ProductionStatusID { get; set; }
        public string ProductionStatusName { get; set; }

        public ProductionStatus() { }
    }
}
