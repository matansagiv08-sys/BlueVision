using Server.DAL;

namespace Server.Models
{
    public class ProductionStage
    {
        public int ProductionStageID { get; set; }
        public string ProductionStageName { get; set; }
        public int StageOrder { get; set; }
        public TimeSpan TargetDuration { get; set; }

        // בנאי המגדיר ערכי ברירת מחדל בעת יצירת אובייקט חדש
        public ProductionStage()
        {
            //  ברירת מחדל של שעה אחת לכל תחנה
            TargetDuration = TimeSpan.FromHours(1);
        }

        public List<ProductionStage> GetProductionStages()
        {
            DBservices dbs = new DBservices();
            return dbs.GetProductionStages();
        }
    }
}
