using Server.DAL;

namespace Server.Models
{
    public class ProductionStatus
    {
        public byte ProductionStatusID { get; set; }
        public string ProductionStatusName { get; set; }

        public ProductionStatus() { }
    }
}
