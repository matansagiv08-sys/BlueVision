using Server.DAL;

// Represents fixed production status values from the database (lookup table)

namespace Server.Models
{
    public class ProductionStatus
    {
        public int ProductionStatusID { get; set; }
        public string ProductionStatusName { get; set; }

        public ProductionStatus() { }

        // שליפת רשימת כל הסטטוסים 
        public List<ProductionStatus> GetProductionStatuses()
        {
            DBservices dbs = new DBservices();
            return dbs.GetProductionStatuses();
        }
    }
}