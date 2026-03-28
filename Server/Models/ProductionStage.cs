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

    //    public List<ProductionStage> GetStages()
    //    {
    //        DBservices dbs = new DBservices();
    //        return dbs.GetStages();
    //    }
    //    public int Insert()
    //    {
    //        DBservices dbs = new DBservices();
    //        return dbs.InsertStage(this);
    //    }

    //    public int Update()
    //    {
    //        DBservices dbs = new DBservices();
    //        return dbs.UpdateStage(this);
    //    }
    }
}
