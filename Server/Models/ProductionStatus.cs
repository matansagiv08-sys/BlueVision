using Server.DAL;

namespace Server.Models
{
    public class ProductionStatus
    {
        public byte ProductionStatusID { get; set; }
        public string ProductionStatusName { get; set; }

        public ProductionStatus() { }

        //public List<ProductionStatus> GetStatuses()
        //{
        //    DBservices dbs = new DBservices();
        //    return dbs.GetStatuses();
        //}

        //public int Insert()
        //{
        //    DBservices dbs = new DBservices();
        //    return dbs.InsertStatus(this);
        //}
    }
}
