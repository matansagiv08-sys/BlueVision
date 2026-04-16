using Server.DAL;

namespace Server.Models
{
    public class ProductionItemStage
    {
        public ProductionStage Stage { get; set; }
        public ProductionStatus Status { get; set; }
        public DateTime? StartTimeStamp { get; set; }
        public DateTime? FinishTimeStamp { get; set; }
        public int? ManualPriority { get; set; }
        public string Comment { get; set; }

        // The ? after DateTime indicates that these properties are nullable, allowing them to be null if the timestamps are not set yet.

        // Constructor to initializes Stage and Status to avoid null values
        public ProductionItemStage()
        {
            this.Stage = new ProductionStage();
            this.Status = new ProductionStatus();
        }

        public int UpdateAllManualPriorities(List<dynamic> updates)
        {
            DBservices dbs = new DBservices();
            int count = 0;

            foreach (var update in updates)
            {
                try
                {
                    // שימוש ב-GetProperty במידה והאובייקט מגיע כ-JsonElement
                    // או המרה ישירה אם זה dynamic רגיל:
                    int serial = Convert.ToInt32(update.serial);
                    string itemId = update.itemId.ToString();
                    int stageId = Convert.ToInt32(update.stageId);
                    int newPriority = Convert.ToInt32(update.newPriority);

                    count += dbs.UpdateManualPriority(serial, itemId, stageId, newPriority);
                }
                catch (Exception ex)
                {
                    // הדפסה לדיבאג כדי לראות איזה שדה נכשל
                    System.Diagnostics.Debug.WriteLine("Error parsing update: " + ex.Message);
                }
            }
            return count;
        }
    }
}
