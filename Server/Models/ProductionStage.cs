using Server.DAL;

namespace Server.Models
{
    public class ProductionStage
    {
        public int ProductionStageID { get; set; }
        public string ProductionStageName { get; set; }
        public TimeSpan TargetDuration { get; set; }
        public int StageOrder { get; set; }

        public ProductionStage() { }


        // Calls DBservices to fetch and return all production stages from the database
        public List<ProductionStage> GetProductionStages()
        {
            DBservices dbs = new DBservices();
            return dbs.GetProductionStages();
        }
    }
}
