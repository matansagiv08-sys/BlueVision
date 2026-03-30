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

        public List<ProductionStage> GetProductionStages()
        {
            DBservices dbs = new DBservices();
            return dbs.GetProductionStages();
        }
    }
}
