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

        public int UpdateAllManualPriorities(List<ManualPriorityUpdateRequest> updates)
        {
            DBservices dbs = new DBservices();
            int count = 0;

            foreach (var update in updates)
            {
                try
                {
                    int serial = update.Serial;
                    string itemId = update.ItemId?.Trim() ?? string.Empty;
                    int stageId = update.StageId;
                    int newPriority = update.NewPriority ?? 0;

                    if (serial <= 0 || string.IsNullOrWhiteSpace(itemId) || stageId <= 0)
                    {
                        continue;
                    }

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

    public class ManualPriorityUpdateRequest
    {
        public int Serial { get; set; }
        public string? ItemId { get; set; }
        public int StageId { get; set; }
        public int? NewPriority { get; set; }
    }
}
