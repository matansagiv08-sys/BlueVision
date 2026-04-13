using Server.DAL;

namespace Server.Models
{
    public class ProductionItemStage
    {
        public ProductionStage Stage { get; set; }
        public ProductionStatus Status { get; set; }
        public DateTime? StartTimeStamp { get; set; }
        public DateTime? FinishTimeStamp { get; set; }
        public string Comment { get; set; }

        // The ? after DateTime indicates that these properties are nullable, allowing them to be null if the timestamps are not set yet.

        // Constructor to initializes Stage and Status to avoid null values
        public ProductionItemStage()
        {
            this.Stage = new ProductionStage();
            this.Status = new ProductionStatus();
        }
    }
}
